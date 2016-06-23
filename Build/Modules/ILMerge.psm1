$script:ilMergeModule = @{}
$script:ilMergeModule.ilMergePath = $null

<# see this aricle: http://www.mattwrock.com/post/2012/02/29/What-you-should-know-about-running-ILMerge-on-Net-45-Beta-assemblies-targeting-Net-40.aspx #>
function Merge-Assemblies {
	Param(
		$files,
		$outputFile,
		$exclude,
		$keyfile,
		[String[]]
		$libPaths
	)

	$exclude | out-file ".\exclude.txt"
	
	$libPathArgs = @()
	
	foreach ($libPath in $libPaths) {
		$libPathArgs = $libPathArgs + "/lib:$libPath"
	}

	$args = @(
		"/internalize:exclude.txt", 
		"/xmldocs",
		"/wildcards",
        "/parallel",
		"/out:$outputFile"
		) + $libPathArgs + $files

	if($ilMergeModule.ilMergePath -eq $null)
	{
		write-error "IlMerge Path is not defined. Please set variable `$ilMergeModule.ilMergePath"
	}
    
	& $ilMergeModule.ilMergePath $args 

	if($LastExitCode -ne 0) {
		write-error "Merge Failed"
	}
	
	remove-item ".\exclude.txt"
}

Export-ModuleMember -Variable "ilMergeModule" -Function "Merge-Assemblies" 