#tool "nuget:?package=xunit.runner.console"
#tool "nuget:?package=GitVersion.CommandLine"

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

Task("SyncNugetDependencies").Does(() => {
	
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
    .IsDependentOn("Restore-NuGet-Packages")
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

Task("Run-Unit-Tests")
    .Does(() =>
{
    XUnit2("./Tests/LiquidProjections.Specs/**/bin/" + configuration + "/*.Specs.dll", new XUnit2Settings {
        });

	XUnit2("./Tests/LiquidProjections.RavenDB.Specs/**/bin/" + configuration + "/*.Specs.dll", new XUnit2Settings {
        });

	XUnit2("./Tests/LiquidProjections.NHibernate.Specs/**/bin/" + configuration + "/*.Specs.dll", new XUnit2Settings {
	});
	
	XUnit2("./Tests/LiquidProjections.NEventStore.Specs/**/bin/" + configuration + "/*.Specs.dll", new XUnit2Settings {
	});
});

Task("Pack")
    .IsDependentOn("GitVersion")
	.IsDependentOn("Build")
    .Does(() => 
    {
      NuGetPack("./src/LiquidProjections.Abstractions/.nuspec", new NuGetPackSettings {
        OutputDirectory = "./Artifacts",
        Version = gitVersion.NuGetVersionV2,
		Properties = new Dictionary<string, string> {
			{ "nugetversion", gitVersion.NuGetVersionV2 }
		}
      });        

      NuGetPack("./src/LiquidProjections/.nuspec", new NuGetPackSettings {
        OutputDirectory = "./Artifacts",
        Version = gitVersion.NuGetVersionV2,
		Properties = new Dictionary<string, string> {
			{ "nugetversion", gitVersion.NuGetVersionV2 }
		}
      });        
	  
	  NuGetPack("./src/LiquidProjections.NEventStore/.nuspec", new NuGetPackSettings {
        OutputDirectory = "./Artifacts",
        Version = gitVersion.NuGetVersionV2,
		Properties = new Dictionary<string, string> {
			{ "nugetversion", gitVersion.NuGetVersionV2 }
		}
      });  

      NuGetPack("./src/LiquidProjections.NHibernate/.nuspec", new NuGetPackSettings {
        OutputDirectory = "./Artifacts",
        Version = gitVersion.NuGetVersionV2,
		Properties = new Dictionary<string, string> {
			{ "nugetversion", gitVersion.NuGetVersionV2 }
		}
      });        
	  
	  NuGetPack("./src/LiquidProjections.RavenDB/.nuspec", new NuGetPackSettings {
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
	.IsDependentOn("GitVersion")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);