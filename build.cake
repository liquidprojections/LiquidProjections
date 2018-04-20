#tool "nuget:?package=xunit.runner.console"
#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=ILRepack"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var toolpath = Argument("toolpath", @"");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./Artifacts") + Directory(configuration);
GitVersion gitVersion = null; 

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);
});

Task("GitVersion").Does(() => {
    gitVersion = GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true
	});
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore("./LiquidProjections.sln", new NuGetRestoreSettings 
	{ 
		NoCache = true,
		Verbosity = NuGetVerbosity.Detailed,
		ToolPath = "./build/nuget.exe"
	});
});

Task("Build")
    .IsDependentOn("GitVersion")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild("./LiquidProjections.sln", settings => {
		settings.ToolPath = String.IsNullOrEmpty(toolpath) ? settings.ToolPath : toolpath;
		settings.ToolVersion = MSBuildToolVersion.VS2017;
        settings.PlatformTarget = PlatformTarget.MSIL;
		settings.SetConfiguration(configuration);
	  });
    }
    else
    {
      // Use XBuild
      XBuild("./LiquidProjections.sln", settings =>
        settings.SetConfiguration(configuration));
    }
});

Task("Merge")
    .IsDependentOn("Build")
	.Does(() => 
	{
		ILRepack(
			"./Artifacts/LiquidProjections.Owin.dll",
			"./Src/LiquidProjections.Owin/bin/" + configuration + "/LiquidProjections.Owin.dll",
			new FilePath[] {
				"./Src/LiquidProjections.Owin/bin/" + configuration + "/Microsoft.Owin.dll",
				"./Src/LiquidProjections.Owin/bin/" + configuration + "/Nancy.dll",
				"./Src/LiquidProjections.Owin/bin/" + configuration + "/Nancy.Metadata.Modules.dll",
				"./Src/LiquidProjections.Owin/bin/" + configuration + "/Nancy.Owin.dll",
				"./Src/LiquidProjections.Owin/bin/" + configuration + "/Nancy.Linker.dll",
				"./Src/LiquidProjections.Owin/bin/" + configuration + "/Nancy.Swagger.dll",
				"./Src/LiquidProjections.Owin/bin/" + configuration + "/Swagger.ObjectModel.dll"
			},
			new ILRepackSettings 
			{ 
				Internalize = true,
				XmlDocs = true
			});
			
		CopyFile("./Artifacts/LiquidProjections.Owin.dll", "./Tests/LiquidProjections.Specs/bin/" + configuration +"/LiquidProjections.Owin.dll");
	});

Task("Run-Unit-Tests")
    .Does(() =>
{
    XUnit2("./Tests/LiquidProjections.Specs/**/bin/" + configuration + "/*.Specs.dll", new XUnit2Settings {
        });
});

Task("Pack")
    .IsDependentOn("GitVersion")
	.IsDependentOn("Merge")
    .Does(() => 
    {
      NuGetPack("./src/LiquidProjections/.nuspec", new NuGetPackSettings {
        OutputDirectory = "./Artifacts",
        Version = gitVersion.NuGetVersionV2,
		Properties = new Dictionary<string, string> {
			{ "nugetversion", gitVersion.NuGetVersionV2 }
		}
      });        

      NuGetPack("./src/LiquidProjections.Owin/.nuspec", new NuGetPackSettings {
        OutputDirectory = "./Artifacts",
        Version = gitVersion.NuGetVersionV2,
		Properties = new Dictionary<string, string> {
			{ "nugetversion", gitVersion.NuGetVersionV2 }
		}
      });        
	  
	  NuGetPack("./src/LiquidProjections.Testing/.nuspec", new NuGetPackSettings {
        OutputDirectory = "./Artifacts",
        Version = gitVersion.NuGetVersionV2,
				Properties = new Dictionary<string, string> {
			{ "nugetversion", gitVersion.NuGetVersionV2 }
		}
      });  
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
	.IsDependentOn("Restore-NuGet-Packages")
	.IsDependentOn("Merge")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);