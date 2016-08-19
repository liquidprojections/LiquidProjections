properties { 
	$BaseDirectory = Resolve-Path .. 
    
    $ProjectName = "LiquidProjections"
    $NuGetPackageBaseName = "LiquidProjections"
    
	$SrcDir = "$BaseDirectory\src"
    $TestsDir = "$BaseDirectory\tests"
    $ArtifactsDirectory = "$BaseDirectory\Artifacts"
    $SolutionFilePath = "$BaseDirectory\$ProjectName.sln"
	$AssemblyInfoFilePath = "$SrcDir\SharedAssemblyInfo.cs"
    $ilMergeModule.ilMergePath = "$BaseDirectory\Packages\ILRepack.2.0.10\tools\ILRepack.exe"

    $NugetExe = "$BaseDirectory\lib\nuget.exe"
    $GitVersionExe = "$BaseDirectory\lib\GitVersion.exe"
}

task default -depends Clean, RestoreNugetPackages, ApplyAssemblyVersioning, ConsolidateNuspecDependencyVersions, Compile, RunTests, CreateNuGetPackages 

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

task ApplyAssemblyVersioning -depends ExtractVersionsFromGit {
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

task ConsolidateNuspecDependencyVersions -depends ExtractVersionsFromGit -precondition { return $NuGetPackageBaseName; } {
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
	exec { msbuild /nologo /verbosity:minimal $SolutionFilePath /p:Configuration=Release }
}

task RunTests -depends Compile -Description "Running all unit tests." {
	$xunitRunner = "$BaseDirectory\packages\xunit.runner.console.2.1.0\tools\xunit.console.exe"
    
    if (!(Test-Path $ArtifactsDirectory)) {
		New-Item $ArtifactsDirectory -Type Directory
	}

	exec { . $xunitRunner `
        "$TestsDir\LiquidProjections.Specs\bin\Release\LiquidProjections.Specs.dll" `
        "$TestsDir\LiquidProjections.NEventStore.Specs\bin\Release\LiquidProjections.NEventStore.Specs.dll" `
        "$TestsDir\LiquidProjections.RavenDB.Specs\bin\Release\LiquidProjections.RavenDB.Specs.dll" `
        -html "$ArtifactsDirectory\xunit.html"  }
}

task MergeAssemblies -depends Compile -Description "Merging dependencies" {

    Merge-Assemblies -outputFile "$ArtifactsDirectory\LiquidProjections.dll" -libPaths "$SrcDir\LiquidProjections\bin\release" -files @(
        "$SrcDir\LiquidProjections\bin\release\LiquidProjections.dll"
    )

    Merge-Assemblies -outputFile "$ArtifactsDirectory\LiquidProjections.NEventStore.dll" -libPaths "$SrcDir\LiquidProjections\bin\release" -files @(
        "$SrcDir\LiquidProjections.NEventStore\bin\release\LiquidProjections.NEventStore.dll"
    )

    Merge-Assemblies -outputFile "$ArtifactsDirectory\LiquidProjections.RavenDB.dll" -libPaths "$SrcDir\LiquidProjections\bin\release" -files @(
        "$SrcDir\LiquidProjections.RavenDB\bin\release\LiquidProjections.RavenDB.dll"
    )
}

task CreateNuGetPackages -depends Compile, MergeAssemblies -Description "Creating NuGet package." {
	gci $SrcDir -Recurse -Include *.nuspec | % {
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
