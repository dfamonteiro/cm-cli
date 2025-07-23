using Cmf.CLI.Constants;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Interfaces;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Factories;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Cmf.CLI.Commands
{
    /// <summary>
    ///
    /// </summary>
    /// <seealso cref="BaseCommand" />
    [CmfCommand(name: "mes", Id = "bump_mes", ParentId = "bump")]
    public class BumpMESCommand : BaseCommand
    {
        /// <summary>
        /// Configure command
        /// </summary>
        /// <param name="cmd"></param>
        public override void Configure(Command cmd)
        {
            cmd.AddArgument(new Argument<DirectoryInfo>(
                name: "packagePath",
                getDefaultValue: () => { return new("."); },
                description: "Package path"));

            cmd.AddArgument(new Argument<string>(
                name: "MESVersion",
                description: "New MES Version"
            ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "-iot", "--iotVersion" },
                description: "New MES version for the IoT workflows & masterdatas."
            ));

            cmd.AddOption(new Option<List<string>>(
                aliases: new string[] {"-ignore", "--iotPackagesToIgnore" },
                description: "IoT packages to ignore when updating the MES version of the tasks in IoT workflows"
            )
            {
                AllowMultipleArgumentsPerToken = true,
            });

            // Add the handler
            cmd.Handler = CommandHandler.Create<DirectoryInfo, string, string, List<string>>(Execute);
        }

        /// <summary>
        /// Executes the specified package path.
        /// </summary>
        /// <param name="packagePath">The package path.</param>
        /// <param name="MESVersion">The new MES version.</param>
        /// <param name="iotVersion">New MES version for the IoT workflows & masterdata</param>
        /// <param name="iotPackagesToIgnore">IoT packages to ignore when updating the MES version of the tasks in IoT workflows</param>
        /// <exception cref="CliException"></exception>
        public void Execute(DirectoryInfo packagePath, string MESVersion, string iotVersion, List<string> iotPackagesToIgnore)
        {
            using var activity = ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var cmfPackagePaths = this.fileSystem.DirectoryInfo.Wrap(packagePath).GetFiles("cmfpackage.json", SearchOption.AllDirectories);

            foreach (IFileInfo path in cmfPackagePaths)
            {
                Log.Debug($"Processing {path.FullName}");
                Execute(CmfPackage.Load(path), MESVersion, iotVersion, iotPackagesToIgnore);
            }
        }

        /// <summary>
        /// Executes the specified CMF package.
        /// </summary>
        /// <param name="cmfPackage">The CMF package.</param>
        /// <param name="version">The version.</param>
        /// <param name="iotVersion">New MES version for the IoT workflows & masterdata</param>
        /// <param name="iotPackagesToIgnore">IoT packages to ignore when updating the MES version of the tasks in IoT workflows</param>
        /// <exception cref="CliException"></exception>
        public void Execute(CmfPackage cmfPackage, string version, string iotVersion, List<string> iotPackagesToIgnore)
        {
            IDirectoryInfo packageDirectory = cmfPackage.GetFileInfo().Directory;
            IPackageTypeHandler packageTypeHandler = PackageTypeFactory.GetPackageTypeHandler(cmfPackage);

            packageTypeHandler.MESBump(version, iotVersion, iotPackagesToIgnore);

            // will save with new version
            cmfPackage.SaveCmfPackage();
        }
    }
}