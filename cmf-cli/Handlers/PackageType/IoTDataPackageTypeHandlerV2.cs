
using Cmf.CLI.Core;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cmf.CLI.Handlers
{
    /// <summary>
    ///
    /// </summary>
    /// <seealso cref="DataPackageTypeHandlerV2" />
    public class IoTDataPackageTypeHandlerV2 : DataPackageTypeHandlerV2
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataPackageTypeHandlerV2" /> class.
        /// </summary>
        /// <param name="cmfPackage">The CMF package.</param>
        public IoTDataPackageTypeHandlerV2(CmfPackage cmfPackage) : base(cmfPackage)
        {
        }

        /// <summary>
        /// Bumps the specified CMF package.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <param name="buildNr">The version for build Nr.</param>
        /// <param name="bumpInformation">The bump information.</param>
        public override void Bump(string version, string buildNr, Dictionary<string, object> bumpInformation = null)
        {
            base.Bump(version, buildNr, bumpInformation);
            // Get All AutomationWorkflowFiles Folders
            List<string> automationWorkflowDirectory = this.fileSystem.Directory.GetDirectories(CmfPackage.GetFileInfo().DirectoryName, "AutomationWorkflowFiles", SearchOption.AllDirectories).ToList();

            // Get Parent Root
            IDirectoryInfo parentRootDirectory = FileSystemUtilities.GetPackageRootByType(CmfPackage.GetFileInfo().DirectoryName, PackageType.Root, this.fileSystem);
            CmfPackageCollection cmfPackageIoT = parentRootDirectory.LoadCmfPackagesFromSubDirectories(packageType: PackageType.IoT);

            #region GetCustomPackages

            // Get Dev Tasks
            string packageNames = null;
            foreach (CmfPackage iotPackage in cmfPackageIoT)
            {
                packageNames += GetCustomPackages(iotPackage);
            }

            #endregion GetCustomPackages

            #region Filter by Root

            if (bumpInformation.ContainsKey("root") && !String.IsNullOrEmpty(bumpInformation["root"] as string))
            {
                string root = bumpInformation["root"] as string;
                if (!automationWorkflowDirectory.Any())
                {
                    Log.Warning($"No AutomationWorkflowFiles found in root {root}");
                }
                // Get All AutomationWorkflowFiles Folders that are under root
                automationWorkflowDirectory = automationWorkflowDirectory.Where(awf => awf.Contains(root))?.ToList() ?? new();
            }

            #endregion Filter by Root

            foreach (string automationWorkflowFileGroup in automationWorkflowDirectory)
            {
                #region Bump AutomationWorkflow

                // Get All Group Folders
                List<string> groups = this.fileSystem.Directory.GetDirectories(automationWorkflowFileGroup, "*").ToList();

                groups.ForEach(group => IoTUtilities.BumpWorkflowFiles(group, version, buildNr, null, packageNames, this.fileSystem));

                #endregion Bump AutomationWorkflow

                #region Bump IoT Masterdata

                IoTUtilities.BumpIoTMasterData(automationWorkflowFileGroup, version, buildNr, this.fileSystem, packageNames, onlyCustomization: true);

                #endregion Bump IoT Masterdata
            }
        }

        /// <summary>
        /// Bumps the MES version of the package
        /// </summary>
        /// <param name="version">The new MES version.</param>
        public override void MESBump(string version, string iotVersion, List<string> iotPackagesToIgnore)
        {
            base.MESBump(version, iotVersion, iotPackagesToIgnore);

            if (iotVersion == null)
            {
                return;
            }

            List<string> ignorePackages = new List<string>()
            {
                "@criticalmanufacturing/connect-iot-controller-engine-custom-utilities-tasks", // SMT Template
                "@criticalmanufacturing/connect-iot-controller-engine-custom-smt-utilities-tasks", // SMT Template
                "@criticalmanufacturing/connect-iot-utilities-semi-tasks", // Semi Template
            };
            ignorePackages.AddRange(iotPackagesToIgnore ?? []);

            // Useful debug info
            Log.Debug("Packages that will be ignored:");
            ignorePackages.ForEach(pkg => Log.Debug($"  - {pkg}"));

            List<string> mdlFiles = new List<string>();
            List<string> workflowFiles = new List<string>();

            foreach (ContentToPack contentToPack in this.CmfPackage.ContentToPack ?? [])
            {
                if (contentToPack.ContentType == ContentType.MasterData)
                {
                    mdlFiles.AddRange(this.fileSystem.Directory.GetFiles(
                        this.CmfPackage.GetFileInfo().DirectoryName,
                        contentToPack.Source,
                        SearchOption.AllDirectories
                    ));
                }
                else if (contentToPack.ContentType == ContentType.AutomationWorkFlows)
                {
                    workflowFiles.AddRange(this.fileSystem.Directory.GetFiles(
                        this.CmfPackage.GetFileInfo().DirectoryName,
                        contentToPack.Source,
                        SearchOption.AllDirectories
                    ));
                }
            }

            if (mdlFiles.Where(mdl => !mdl.EndsWith(".json")).Any())
            {
                Log.Warning("Only .json masterdata files will be updated");
            }

            // Update the MES references that might be present in the mdl files
            foreach (string mdlPath in mdlFiles.Where(mdl => mdl.EndsWith(".json")))
            {
                // Update some versions in several places in the masterdata
                string text = this.fileSystem.File.ReadAllText(mdlPath);
                foreach (string key in new string[] { "PackageVersion", "ControllerPackageVersion", "MonitorPackageVersion", "ManagerPackageVersion" })
                {
                    text = MESBumpUtilities.UpdateJsonValue(text, key, iotVersion);
                }

                // Updating the versions in <DM>AutomationController requires special handling
                JObject packageJsonObject = JsonConvert.DeserializeObject<JObject>(text);
                JObject automationControllers = packageJsonObject["<DM>AutomationController"] as JObject;

                foreach (var prop in automationControllers.Properties())
                {
                    JObject controller = (JObject)prop.Value;
                    string tasksLibraryPackagesRaw = controller["TasksLibraryPackages"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(tasksLibraryPackagesRaw))
                    {
                        // Parse the tasksLibraryPackages json list
                        JArray tasksLibraryPackages = JsonConvert.DeserializeObject<JArray>(tasksLibraryPackagesRaw);

                        // Version bump each package string
                        for (int i = 0; i < tasksLibraryPackages.Count; i++)
                        {
                            string packageStr = tasksLibraryPackages[i]?.ToString();

                            if (string.IsNullOrEmpty(packageStr))
                            {
                                continue;
                            }

                            if (ignorePackages.Any(ignore => packageStr.Contains(ignore)))
                            {
                                continue; // If there's a match with a package in the ignorePackages, skip the version bump
                            }

                            tasksLibraryPackages[i] = Regex.Replace(packageStr, @"@\d+.+$", $"@{iotVersion}");
                        }

                        // Save it back into the controller object
                        controller["TasksLibraryPackages"] = JsonConvert.SerializeObject(tasksLibraryPackages, Formatting.None);
                    }
                }

                this.fileSystem.File.WriteAllText(mdlPath, JsonConvert.SerializeObject(packageJsonObject, Formatting.Indented));
            }

            // Update the IoT workflows
            foreach (string wflPath in workflowFiles)
            {
                string packageJson = fileSystem.File.ReadAllText(wflPath);
                dynamic packageJsonObject = JsonConvert.DeserializeObject(packageJson);

                foreach (var task in packageJsonObject?["tasks"])
                {
                    string name = (string)task["reference"]["package"]["name"];
                    if (ignorePackages.Any(ignore => name.Contains(ignore)))
                    {
                        continue; // If there's a match with a package in the ignorePackages, skip the version bump
                    }

                    task["reference"]["package"]["version"] = iotVersion;
                }

                foreach (var converter in packageJsonObject?["converters"])
                {
                    string name = (string)converter["reference"]["package"]["name"];
                    if (ignorePackages.Any(ignore => name.Contains(ignore)))
                    {
                        continue; // If there's a match with a package in the ignorePackages, skip the version bump
                    }

                    converter["reference"]["package"]["version"] = iotVersion;
                }

                fileSystem.File.WriteAllText(wflPath, JsonConvert.SerializeObject(packageJsonObject, Formatting.Indented));
            }
        }

        /// <summary>
        /// Retrieves all custom iot package names
        /// </summary>
        /// <param name="iotPackage"></param>
        /// <returns></returns>
        private string GetCustomPackages(CmfPackage iotPackage)
        {
            string targetDirectory = ".dev-tasks.json";
            string targetProperties = "packages";

            if (ExecutionContext.Instance.ProjectConfig.MESVersion.Major > 10)
            {
                targetDirectory = "package.json";
                targetProperties = "workspaces";
            }

            string packagesFile = this.fileSystem.Directory.GetFiles(iotPackage.GetFileInfo().DirectoryName, targetDirectory).FirstOrDefault();

            string contentJson = this.fileSystem.File.ReadAllText(packagesFile);
            dynamic contentObject = JsonConvert.DeserializeObject(contentJson);

            string packageNames = string.IsNullOrEmpty(contentObject["packagesBuildBump"]?.ToString()) ? contentObject[targetProperties]?.ToString() : contentObject["packagesBuildBump"]?.ToString();

            return packageNames;
        }
    }
}