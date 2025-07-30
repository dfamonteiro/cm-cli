
using Cmf.CLI.Commands;
using Cmf.CLI.Constants;
using Cmf.CLI.Core.Interfaces;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Factories;
using Cmf.CLI.Handlers;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.RegularExpressions;
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

        BumpMESCommand cmd = new BumpMESCommand(fileSystem);
        cmd.Execute(fileSystem.FileInfo.New(projectConfigPath).Directory, "11.1.6", "11.1.6", []);

        string projectConfigContents = fileSystem.File.ReadAllText(projectConfigPath);

        projectConfigContents.Should().ContainAll([
            @"""MESVersion"": ""11.1.6"",",
            @"""NugetVersion"": ""11.1.6"",",
            @"""TestScenariosNugetVersion"": ""11.1.6"",",
        ]);
        Assert.Equal(3, Regex.Matches(projectConfigContents, @"11\.1\.6").Count);
    }

    [Theory]
    [InlineData(@"c:\\Builds\.vars\global.yml", "10.2.12")]
    [InlineData(@"c:\\Builds\.vars\abc.yml", "10.2.12")]
    [InlineData(@"c:\\EnvironmentConfigs\global.yml", "11.1.6")]
    public void BumpMES_PipelineFiles(string path, string version)
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            {
                MockUnixSupport.Path(path),
                new MockFileData(
                    @"variables:
                        CIPublishArtifacts: 'a\b\c'
                        CIPackages: 'a\b\c'
                        ApprovedPackages: 'a\b\c'
                        CmfCliRepository: 'https://registry.npmjs.com/'
                        CmfCliVersion: 'latest'
                        CmfCliPath: '$(Agent.TempDirectory)/node_modules/.bin/cmf-cli'
                        CmfPipelineRepository: 'abc'
                        CmfPipelineVersion: 'latest'
                        CmfPipelinePath: '$(Agent.TempDirectory)/node_modules/.bin/@criticalmanufacturing/pipeline'

                        ISOImagePath: '\\management\Setups\cmNavigo\v11.1.x\Critical Manufacturing 10.2.4.iso'
                        CommonBranch: 'refs/tags/10.2.5'

                        DeploymentPackage: '@criticalmanufacturing\mes:10.2.2'
                        IoTPackage: '@criticalmanufacturing\connectiot-manager:10.2.1'
                        "
                )
            }
        });

        BumpMESCommand cmd = new BumpMESCommand(fileSystem);
        cmd.Execute(fileSystem.DirectoryInfo.New(@"c:\\"), version, version, []);
        
        string projectConfigContents = fileSystem.File.ReadAllText(path);

        projectConfigContents.Should().ContainAll([
            $@"CommonBranch: 'refs/tags/{version}'",
            $@"DeploymentPackage: '@criticalmanufacturing\mes:{version}'",
            $@"IoTPackage: '@criticalmanufacturing\connectiot-manager:{version}'",
        ]);

        int major = new Version(version).Major;
        int minor = new Version(version).Minor;
        string optionalServices = major <= 10 ? "" : "-Optional Services";

        projectConfigContents.Should().Contain(
            $@"ISOImagePath: '\\management\Setups\cmNavigo\v{major}.{minor}.x\Critical Manufacturing {version}{optionalServices}.iso'"
        );

        Assert.Equal(4, Regex.Matches(projectConfigContents, version.Replace(".", "\\.")).Count);
    }
}