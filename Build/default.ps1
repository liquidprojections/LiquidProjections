properties { 
	$BaseDirectory = Resolve-Path .. 
    
    $ProjectName = "Liquid.Projections"
    
	$SrcDir = "$BaseDirectory\src"
    $TestsDir = "$BaseDirectory\tests"
    $ArtifactsDirectory = "$BaseDirectory\Artifacts"
    $SolutionFilePath = "$BaseDirectory\$ProjectName.sln"
	$AssemblyInfoFilePath = "$SrcDir\SharedAssemblyInfo.cs"
    $ilMergeModule.ilMergePath = "$BaseDirectory\Packages\ILRepack.2.0.10\tools\ILRepack.exe"

    $NugetExe = "$BaseDirectory\lib\nuget.exe"
    $GitVersionExe = "$BaseDirectory\lib\GitVersion.exe"
}

task default -depends Clean, ExtractVersionsFromGit, RestoreNugetPackages, ApplyAssemblyVersioning, Compile, RunTests, MergeAssemblies, CreateNuGetPackages 

task RestoreNugetPackages {
    $packageConfigs = Get-ChildItem "$BaseDirectory" -Recurse | where{$_.Name -eq "packages.config"}

    foreach($packageConfig in $packageConfigs){
    	Write-Host "Restoring" $packageConfig.FullName 
    	exec { 
            . "$NugetExe" install $packageConfig.FullName -OutputDirectory "$BaseDirectory\packages" -NonInteractive
        }
    }
}

task Clean -Description "Cleaning solution." {
	Remove-Item $ArtifactsDirectory/* -Force -Recurse -ErrorAction SilentlyContinue
	exec { msbuild /nologo /verbosity:minimal $SolutionFilePath /t:Clean  }
    
    if (!(Test-Path -Path $ArtifactsDirectory)) {
        New-Item -ItemType Directory -Force -Path $ArtifactsDirectory
    }
}

task ExtractVersionsFromGit {
    
        $json = . "$GitVersionExe" /u $GitVersionUsername /p $GitVersionPassword 
        
        if ($LASTEXITCODE -eq 0) {
            $version = (ConvertFrom-Json ($json -join "`n"));
          
            $script:AssemblyVersion = $version.AssemblySemVer;
            $script:InformationalVersion = $version.InformationalVersion;
            $script:NuGetVersion = $version.NuGetVersionV2;
        }
        else {
            Write-Output $json -join "`n";
        }
}

task ApplyAssemblyVersioning {
	Get-ChildItem -Path $BaseDirectory -Filter "?*AssemblyInfo.cs" -Recurse -Force |
	foreach-object {  

		Set-ItemProperty -Path $_.FullName -Name IsReadOnly -Value $false

        $content = Get-Content $_.FullName
        
        if ($script:AssemblyVersion) {
    		Write-Output "Updating " $_.FullName "with version" $script:AssemblyVersion
    	    $content = $content -replace 'AssemblyVersion\("(.+)"\)', ('AssemblyVersion("' + $script:AssemblyVersion + '")')
            $content = $content -replace 'AssemblyFileVersion\("(.+)"\)', ('AssemblyFileVersion("' + $script:AssemblyVersion + '")')
        }
		
        if ($script:InformationalVersion) {
    		Write-Output "Updating " $_.FullName "with information version" $script:InformationalVersion
            $content = $content -replace 'AssemblyInformationalVersion\("(.+)"\)', ('AssemblyInformationalVersion("' + $script:InformationalVersion + '")')
        }
        
	    Set-Content -Path $_.FullName $content
	}    
}

task Compile -Description "Compiling solution." { 
	exec { msbuild /nologo /verbosity:minimal $SolutionFilePath /p:Configuration=Release }
}

task RunTests -depends Compile -Description "Running all unit tests." {
	$xunitRunner = "$BaseDirectory\packages\xunit.runner.console.2.1.0\tools\xunit.console.exe"
    
    if (!(Test-Path $ArtifactsDirectory)) {
		New-Item $ArtifactsDirectory -Type Directory
	}

	exec { . $xunitRunner "$TestsDir\Liquid.Projections.Specs\bin\Release\Liquid.Projections.Specs.dll" -html "$ArtifactsDirectory\xunit.html"  }
}

task MergeAssemblies -depends Compile -Description "Merging dependencies" {

    Merge-Assemblies -outputFile "$ArtifactsDirectory\Liquid.Projections.dll" -libPaths "$SrcDir\Liquid.Projections\bin\release" -files @(
        "$SrcDir\Liquid.Projections\bin\release\Liquid.Projections.dll"
    )
}

task CreateNuGetPackages -depends Compile -Description "Creating NuGet package." {
	gci $BaseDirectory -Recurse -Include *.nuspec | % {
		exec { 
			$NuGetVersion = $script:NuGetVersion
			
            if (!$NuGetVersion) {
                $NuGetVersion = "0.0.1.0"
            }
        
        Write-Host $_
            . "$NugetExe" pack $_ -o "$ArtifactsDirectory" -version $NuGetVersion 
        }
	}
}
