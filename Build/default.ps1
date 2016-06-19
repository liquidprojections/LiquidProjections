properties { 
	$ProjectName = "Liquid.Projections"
    $AssemblyVersion = "1.2.3.4"
	$InformationalVersion = "1.2.3-unstable.34+34.Branch.develop.Sha.19b2cd7f494c092f87a522944f3ad52310de79e0"
	$NuGetVersion = "1.2.3-unstable4"
	$RootDir  = Resolve-Path ..\
	$NugetOutputDir = "$RootDir\Output"
	$SrcDir = "$RootDir\Sources"
    $ReportsDir = "$RootDir\TestResults"
	$SolutionFilePath = "$SrcDir\$ProjectName.sln"
	$AssemblyInfoFilePath = "$SrcDir\SharedAssemblyInfo.cs"
    $ilMergeModule.ilMergePath = "$SrcDir\packages\ilmerge.2.14.1208\tools\ILMerge.exe"
	$NuGetPackageBaseName = "eVision.QueryHost"
}

TaskSetup {
    TeamCity-ReportBuildProgress "Starting task $($psake.context.Peek().currentTaskName)"
}

TaskTearDown {
    TeamCity-ReportBuildProgress "Finished task $($psake.context.Peek().currentTaskName)"
}

task default -depends Clean, UpdateVersion, ConsolidateNuspecDependencyVersions, Compile, RunTests, MergeAssemblies, CreateNuGetPackages

task Clean -Description "Cleaning solution." {
	Remove-Item $NugetOutputDir/* -Force -Recurse -ErrorAction SilentlyContinue
	exec { msbuild /nologo /verbosity:minimal $SolutionFilePath /t:Clean /p:VSToolsPath="$SrcDir\Packages\MSBuild.Microsoft.VisualStudio.Web.targets.11.0.2.1\tools\VSToolsPath" }
    
    if (!(Test-Path -Path $NugetOutputDir)) {
        New-Item -ItemType Directory -Force -Path $NugetOutputDir
    }
}

task UpdateVersion -Description "Updating assembly version number." {
	Update-Version $AssemblyVersion $InformationalVersion $AssemblyInfoFilePath
}

task ConsolidateNuspecDependencyVersions -precondition { return $NuGetPackageBaseName; } {
	Write-Host "Updating all NuGet dependencies on $NuGetPackageBaseName.* to version ""$NuGetVersion"""

	Get-ChildItem $SrcDir -Recurse -Include *.nuspec | % {

		$nuspecFile = $_.fullName;
		Write-Host "    $nuspecFile updated"
		
		$tmpFile = $nuspecFile + ".tmp"
		
		Get-Content $nuspecFile | `
        %{$_ -replace "(<dependency\s+id=""$NuGetPackageBaseName.*?"")(\s+version="".+"")?\s*\/>", "`${1} version=""$NuGetVersion""/>" } | `
        Out-File -Encoding UTF8 $tmpFile

		Move-Item $tmpFile $nuspecFile -force
	}
}

task Compile -Description "Compiling solution." { 
	exec { msbuild /nologo /verbosity:minimal $SolutionFilePath /p:Configuration=Release /p:VSToolsPath="$SrcDir\Packages\MSBuild.Microsoft.VisualStudio.Web.targets.11.0.2.1\tools\VSToolsPath" }
}

task RunTests -depends Compile -Description "Running all unit tests." {
    $openCover = "$SrcDir\packages\OpenCover.4.5.3723\OpenCover.Console.exe"
    $reportGenerator = "$SrcDir\packages\ReportGenerator.2.1.1.0\ReportGenerator.exe"
	$xunitRunner = "$SrcDir\packages\xunit.runner.console.2.0.0\tools\xunit.console.exe"

    if(!(Test-Path $ReportsDir)){
	    New-Item $ReportsDir -Type Directory
	}

	Get-ChildItem $SrcDir -Recurse -Include *.Specs.dll | 
		Where-Object { ($_.FullName -notlike "*obj*") -and ($_.FullName -notlike "*TestWebHost*") } | % {
		$project = $_.BaseName
		
		exec {
            . $xunitRunner "$_" -html "$ReportsDir\$project-index.html"
        }
	}
}

task MergeAssemblies -depends Compile -Description "Merging dependencies" {

    Merge-Assemblies -outputFile "$NugetOutputDir/eVision.QueryHost.dll" -files @(
        "$SrcDir/QueryHost/bin/release/eVision.QueryHost.dll",
        "$SrcDir/QueryHost/bin/release/Autofac.dll",
        "$SrcDir/QueryHost/bin/release/Autofac.Integration.WebApi.dll",
        "$SrcDir/QueryHost/bin/release/Newtonsoft.Json.dll",
        "$SrcDir/QueryHost/bin/release/System.Net.Http.Formatting.dll",
        "$SrcDir/QueryHost/bin/release/System.Web.Http.dll",
        "$SrcDir/QueryHost/bin/release/System.Web.Http.Owin.dll",
        "$SrcDir/QueryHost/bin/release/Microsoft.Owin.dll",
        "$SrcDir/QueryHost/bin/release/System.Reactive.Core.dll",
        "$SrcDir/QueryHost/bin/release/System.Reactive.Interfaces.dll",
        "$SrcDir/QueryHost/bin/release/System.Reactive.Linq.dll",
        "$SrcDir/QueryHost/bin/release/System.Reactive.PlatformServices.dll"
    )
    
    Merge-Assemblies -outputFile "$NugetOutputDir/eVision.QueryHost.Client.dll" -files @(
        "$SrcDir/QueryHost.Client/bin/release/eVision.QueryHost.Client.dll"
    )

    Merge-Assemblies -outputFile "$NugetOutputDir/eVision.QueryHost.Raven.dll" -files @(
        "$SrcDir/QueryHost.Raven/bin/release/eVision.QueryHost.Raven.dll",
        "$SrcDir/QueryHost.Raven/bin/release/System.Reactive.Core.dll",
        "$SrcDir/QueryHost.Raven/bin/release/System.Reactive.Interfaces.dll",
        "$SrcDir/QueryHost.Raven/bin/release/System.Reactive.Linq.dll",
        "$SrcDir/QueryHost.Raven/bin/release/System.Reactive.PlatformServices.dll"
    )
    
    Merge-Assemblies -outputFile "$NugetOutputDir/eVision.QueryHost.NEventStore.dll" -files @(
    "$SrcDir/QueryHost.NEventStore/bin/release/eVision.QueryHost.NEventStore.dll"
        
    )
}

task CreateNuGetPackages -depends Compile -Description "Creating NuGet package." {
	gci $SrcDir -Recurse -Include *.nuspec | % {
		exec { ..\Tools\nuget.exe pack $_ -o $NugetOutputDir -version $NuGetVersion }
	}
}
