using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Objects;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Cmf.CLI.Core.Enums;

namespace Cmf.CLI.Utilities
{
    public static class MESBumpUtilities
    {
        /// <summary>
        /// Updates the value of a given key in a JSON-formatted string using a regular expression.
        /// </summary>
        /// <param name="text">The original JSON content as a string.</param>
        /// <param name="key">The JSON key whose value should be updated.</param>
        /// <param name="newValue">The new value to assign to the key.</param>
        /// <returns>The modified JSON string with the updated key value.</returns>
        public static string UpdateJsonValue(string text, string key, string newValue)
        {
            return Regex.Replace(text, $"\"{key}\"" + @".*:.*"".+""", $"\"{key}\": \"{newValue}\"", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Updates all relevant NPM project files by replacing release version tags in package.json
        /// and removing the package-lock.json files to force regeneration.
        /// </summary>
        /// <param name="fileSystem">The file system abstraction used to access and modify files.</param>
        /// <param name="cmfPackage">The CMF package object containing the root directory path.</param>
        /// <param name="version">The new MES version.</param>
        public static void UpdateNPMProject(IFileSystem fileSystem, CmfPackage cmfPackage, string version)
        {
            // package.json files
            string[] filesToUpdate = fileSystem.Directory.GetFiles(cmfPackage.GetFileInfo().DirectoryName, "package.json", SearchOption.AllDirectories);
            string pattern = @"release-\d+";

            foreach (string filePath in filesToUpdate.Where(path => !path.Contains("node_modules") && !path.Contains("dist")))
            {
                string text = fileSystem.File.ReadAllText(filePath);
                text = Regex.Replace(text, pattern, $"release-{version.Replace(".", "")}", RegexOptions.IgnoreCase);

                fileSystem.File.WriteAllText(filePath, text);
            }

            // package-lock.json files
            string[] filesToDelete = fileSystem.Directory.GetFiles(cmfPackage.GetFileInfo().DirectoryName, "package-lock.json", SearchOption.AllDirectories);
            foreach (string filePath in filesToDelete.Where(path => !path.Contains("node_modules") && !path.Contains("dist")))
            {
                Log.Warning($"Package lock {filePath} has been deleted. Please build the {cmfPackage.PackageId} package to regenerate this file");
                fileSystem.File.Delete(filePath);
            }
        }

        /// <summary>
        /// Updates version references to CMF NuGet packages in all .csproj files within the package directory.
        /// </summary>
        /// <param name="fileSystem">The file system abstraction used to access and modify files.</param>
        /// <param name="cmfPackage">The CMF package object containing the root directory path.</param>
        /// <param name="version">The new MES version.</param>
        /// <param name="strictMatching">
        ///     If true, only references to Cmf.Navigo, Cmf.Foundation and Cmf.MessageBus packages will be updated.
        ///     If false, all packages starting with Cmf. will be updated.
        /// </param>
        public static void UpdateCSharpProject(IFileSystem fileSystem, CmfPackage cmfPackage, string version, bool strictMatching)
        {
            string[] filesToUpdate = fileSystem.Directory.GetFiles(cmfPackage.GetFileInfo().DirectoryName, "*.csproj", SearchOption.AllDirectories);
            
            string pattern;
            if (strictMatching)
            {
                // Only update Cmf.Navigo and Cmf.Foundation references
                pattern = @"(Include=""Cmf\.(?:Navigo|Foundation|MessageBus)[^""]*""\s+Version="")(.*?)(""[\s/>])";
            }
            else
            {
                // Only update Cmf.* references
                pattern = @"(Include=""Cmf\.[^""]*""\s+Version="")(.*?)(""[\s/>])";
            }

            foreach (string filePath in filesToUpdate)
            {
                string text = fileSystem.File.ReadAllText(filePath);
                text = Regex.Replace(text, pattern, match =>
                {
                    return match.Groups[1].Value + version + match.Groups[3].Value;
                }, RegexOptions.IgnoreCase);

                fileSystem.File.WriteAllText(filePath, text);
            }
        }

        /// <summary>
        /// Updates version references in IoT master data and automation workflow files
        /// within a given package, skipping specific packages if configured.
        /// </summary>
        /// <param name="fileSystem">The file system abstraction used to access files.</param>
        /// <param name="cmfPackage">The CMF package being processed.</param>
        /// <param name="version">The new MES version.</param>
        /// <param name="iotPackagesToIgnore">List of package names to ignore during the update (e.g., custom tasks).</param>
        public static void UpdateIoTMasterdatasAndWorkflows(IFileSystem fileSystem, CmfPackage cmfPackage, string version, List<string> iotPackagesToIgnore)
        {
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

            foreach (ContentToPack contentToPack in cmfPackage.ContentToPack ?? [])
            {
                if (contentToPack.ContentType == ContentType.MasterData)
                {
                    mdlFiles.AddRange(fileSystem.Directory.GetFiles(
                        cmfPackage.GetFileInfo().DirectoryName,
                        contentToPack.Source,
                        SearchOption.AllDirectories
                    ));
                }
                else if (contentToPack.ContentType == ContentType.AutomationWorkFlows)
                {
                    workflowFiles.AddRange(fileSystem.Directory.GetFiles(
                        cmfPackage.GetFileInfo().DirectoryName,
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
                UpdateIoTMasterdata(mdlPath, fileSystem, cmfPackage, version, ignorePackages);
            }

            Log.Debug("Processing workflows...");
            // Update the IoT workflows
            foreach (string wflPath in workflowFiles.Where(path => path.EndsWith(".json")))
            {
                UpdateIoTWorkflow(wflPath, fileSystem, cmfPackage, version, ignorePackages);
            }
        }

        /// <summary>
        /// Updates version values in a given IoT master data (.json) file.
        /// </summary>
        /// <param name="mdlPath">Path to the master data file.</param>
        /// <param name="fileSystem">The file system abstraction used to access files.</param>
        /// <param name="cmfPackage">The CMF package that owns the master data.</param>
        /// <param name="version">The new MES version.</param>
        /// <param name="ignorePackages">List of task package names to ignore.</param>
        private static void UpdateIoTMasterdata(string mdlPath, IFileSystem fileSystem, CmfPackage cmfPackage, string version, List<string> ignorePackages)
        {
            // Update some versions in several places in the masterdata
            string text = fileSystem.File.ReadAllText(mdlPath);
            foreach (string key in new string[] { "PackageVersion", "ControllerPackageVersion", "MonitorPackageVersion", "ManagerPackageVersion" })
            {
                text = MESBumpUtilities.UpdateJsonValue(text, key, version);
            }

            // Updating the versions in <DM>AutomationController requires special handling
            JObject packageJsonObject = JsonConvert.DeserializeObject<JObject>(text);

            if (packageJsonObject.ContainsKey("<DM>AutomationController"))
            {
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

                            tasksLibraryPackages[i] = Regex.Replace(packageStr, @"@\d+.+$", $"@{version}");
                        }

                        // Save it back into the controller object
                        controller["TasksLibraryPackages"] = JsonConvert.SerializeObject(tasksLibraryPackages, Formatting.None);
                    }
                }
            }

            fileSystem.File.WriteAllText(mdlPath, JsonConvert.SerializeObject(packageJsonObject, Formatting.Indented));
        }

        /// <summary>
        /// Updates the package version references in an IoT automation workflow file, 
        /// skipping any package names included in the ignore list.
        /// </summary>
        /// <param name="wflPath">Path to the workflow .json file.</param>
        /// <param name="fileSystem">The file system abstraction used to access files.</param>
        /// <param name="cmfPackage">The CMF package that owns the workflow.</param>
        /// <param name="version">The new MES version.</param>
        /// <param name="ignorePackages">List of task package names to skip when applying the version update.</param>
        private static void UpdateIoTWorkflow(string wflPath, IFileSystem fileSystem, CmfPackage cmfPackage, string version, List<string> ignorePackages)
        {
            Log.Debug($"  - {wflPath}");
            string packageJson = fileSystem.File.ReadAllText(wflPath);
            dynamic packageJsonObject = JsonConvert.DeserializeObject(packageJson);

            foreach (var task in packageJsonObject?["tasks"])
            {
                string name = (string)task["reference"]["package"]["name"];
                if (ignorePackages.Any(ignore => name.Contains(ignore)))
                {
                    continue; // If there's a match with a package in the ignorePackages, skip the version bump
                }

                task["reference"]["package"]["version"] = version;
            }

            foreach (var converter in packageJsonObject?["converters"])
            {
                string name = (string)converter["reference"]["package"]["name"];
                if (ignorePackages.Any(ignore => name.Contains(ignore)))
                {
                    continue; // If there's a match with a package in the ignorePackages, skip the version bump
                }

                converter["reference"]["package"]["version"] = version;
            }

            fileSystem.File.WriteAllText(wflPath, JsonConvert.SerializeObject(packageJsonObject, Formatting.Indented));
        }
    }
}