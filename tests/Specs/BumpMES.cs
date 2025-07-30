
using Cmf.CLI.Commands;
using Cmf.CLI.Constants;
using Cmf.CLI.Core.Interfaces;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Factories;
using Cmf.CLI.Handlers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace tests.Specs;

public class BumpMES
{
    [Fact]
    public void BumpMES_ProjectConfig()
    {
        string projectConfigPath = @"c:\\.project-config.json";

        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            {
                MockUnixSupport.Path(projectConfigPath),
                new MockFileData(
                    @"{
                          ""ProjectName"": ""SMTTemplate"",
                          ""RepositoryType"": ""Customization"",
                          ""BaseLayer"": ""MES"",
                          ""NPMRegistry"": ""https://google.com"",
                          ""NuGetRegistry"": ""https://google.com"",
                          ""AzureDevopsCollectionURL"": ""https://google.com"",
                          ""AzureDevopsProductURL"": ""https://google.com"",
                          ""RepositoryURL"": ""https://google.com"",
                          ""EnvironmentName"": ""SMTTemplate"",
                          ""DefaultDomain"": ""CMF"",
                          ""RESTPort"": ""443"",
                          ""Tenant"": ""IndustryTemplates"",
                          ""MESVersion"": ""11.1.1"",
                          ""DevTasksVersion"": """",
                          ""HTMLStarterVersion"": """",
                          ""YoGeneratorVersion"": """",
                          ""NGXSchematicsVersion"": ""1.3.4"",
                          ""NugetVersion"": ""11.1.2"",
                          ""TestScenariosNugetVersion"": ""11.1.3"",
                          ""IsSslEnabled"": ""True""
                    }"
                )
            }
        });


        ExecutionContext.ServiceProvider = (new ServiceCollection())
            .AddSingleton<IProjectConfigService>(new ProjectConfigService())
            .BuildServiceProvider();
        ExecutionContext.Initialize(fileSystem);

        fileSystem.Directory.SetCurrentDirectory(fileSystem.FileInfo.New(projectConfigPath).DirectoryName);

        BumpMESCommand cmd = new BumpMESCommand(fileSystem);
        cmd.Execute(fileSystem.FileInfo.New(projectConfigPath).Directory, "11.1.6", "11.1.6", []);

        string projectConfigContents = fileSystem.File.ReadAllText(projectConfigPath);

        projectConfigContents.Should().ContainAll([
            @"""MESVersion"": ""11.1.6"",",
            @"""NugetVersion"": ""11.1.6"",",
            @"""TestScenariosNugetVersion"": ""11.1.6"",",
        ]);
    }
}