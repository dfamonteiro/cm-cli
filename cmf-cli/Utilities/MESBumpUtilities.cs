using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Objects;

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
    }
}