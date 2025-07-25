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
        public static string UpdateJsonValue(string text, string key, string newValue)
        {
            return Regex.Replace(text, $"\"{key}\"" + @".*:.*"".+""", $"\"{key}\": \"{newValue}\"", RegexOptions.IgnoreCase);
        }

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

        public static void UpdateCSharpProject(IFileSystem fileSystem, CmfPackage cmfPackage, string version, bool strictMatching)
        {
            string[] filesToUpdate = fileSystem.Directory.GetFiles(cmfPackage.GetFileInfo().DirectoryName, "*.csproj", SearchOption.AllDirectories);
            
            string pattern;
            if (strictMatching)
            {
                // Only update Cmf.Navigo and Cmf.Foundation references
                pattern = @"(Include=""Cmf\.(?:Navigo|Foundation)[^""]*""\s+Version="")(.*?)(""[\s/>])";
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