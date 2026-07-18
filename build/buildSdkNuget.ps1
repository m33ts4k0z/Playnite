#Requires -Version 7

param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$OutputPath = (Join-Path $PWD "$($Configuration)SDK"),
    [switch]$SkipBuild = $false,
    [string]$LocalPublish
)

$ErrorActionPreference = "Stop"
& .\common.ps1

# -------------------------------------------
#            Compile SDK
# -------------------------------------------
if (!$SkipBuild)
{
    New-EmptyFolder $OutputPath
    $project = Join-Path $pwd "..\source\PlayniteSDK\Playnite.SDK.csproj"
    $msbuildPath = Get-MsBuildPath
    $arguments = "`"$project`" /p:OutputPath=`"$outputPath`";Configuration=$configuration /t:Build"
    $compilerResult = StartAndWait $msbuildPath $arguments
    if ($compilerResult -ne 0)
    {
        throw "Build failed."
    }
}

# -------------------------------------------
#            Create NUGET
# -------------------------------------------
$version = (Get-ChildItem (Join-Path $OutputPath "Playnite.SDK.dll")).VersionInfo.ProductVersion
$version = $version -replace "\.0$", ""
$spec = Get-Content "PlayniteSDK.nuspec"
$spec = $spec -replace "{Version}", $version
$spec = $spec -replace "{OutDir}", $OutputPath
$specFile = "nuget.nuspec"

try
{
    $spec | Out-File $specFile
    $packageRes = Invoke-Nuget "pack $specFile -OutputDirectory $OutputPath"
    if ($packageRes -ne 0)
    {
        throw "Nuget packing failed."
    }
}
finally
{
    Remove-Item $specFile -EA 0
}

# -------------------------------------------
#            Create SDK v7 NUGET
# -------------------------------------------
# SDK 7 is SDK-style and defines its package in the csproj, so msbuild Pack
# replaces the nuspec templating used for SDK 6.
$v7Project = Join-Path $pwd "..\source\PlayniteSDK.V7\Playnite.SDK.V7.csproj"
$v7MsbuildPath = Get-MsBuildPath
$v7Arguments = "`"$v7Project`" /p:Configuration=$Configuration /t:Restore;Pack /p:PackageOutputPath=`"$OutputPath`""
$v7Result = StartAndWait $v7MsbuildPath $v7Arguments
if ($v7Result -ne 0)
{
    throw "SDK v7 nuget packing failed."
}

if ($LocalPublish)
{
    Invoke-Nuget "init `"$pwd`" `"$LocalPublish`""
}

return $true