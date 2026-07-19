#Requires -Version 7
# Converts Playnite WPF localization ResourceDictionaries into Avalonia .axaml
# resource dictionaries for the Avalonia shells' bundled language corpus.
# WPF uses <sys:String x:Key="LOC..">v</sys:String> under the presentation
# namespace; Avalonia uses <x:String x:Key="LOC..">v</x:String> under the
# avaloniaui namespace. Values are re-emitted XML-escaped.
#
# Notes learned during S1.1 (Phase 7 settings parity, language packs):
#  - LocSource.xaml is the authoritative English base (1123 keys with values).
#  - en_US.xaml exports blank source-equal values (~24 non-empty) — skip it.
#  - Translations are partial (e.g. af_ZA ~920 keys), so empty values MUST be
#    skipped here and English (LocSource) merged as the fallback layer at runtime.
#
# Usage:
#   pwsh build/ConvertLocalization.ps1 -SourceDir ..\source\Playnite\Localization -TargetDir <shell>\Localization\Languages
param(
    [Parameter(Mandatory = $true)][string]$SourceDir,
    [Parameter(Mandatory = $true)][string]$TargetDir,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

function ConvertOne([string]$srcPath, [string]$dstPath)
{
    [xml]$doc = Get-Content -Raw -LiteralPath $srcPath
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('<ResourceDictionary xmlns="https://github.com/avaloniaui"')
    [void]$sb.AppendLine('                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">')
    $count = 0
    foreach ($node in $doc.DocumentElement.ChildNodes)
    {
        if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
        if ($node.LocalName -ne "String") { continue }
        $key = $node.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml")
        if ([string]::IsNullOrEmpty($key)) { continue }
        # Skip empty values so they fall back to English rather than blanking the UI.
        if ([string]::IsNullOrWhiteSpace($node.InnerText)) { continue }
        $value = [System.Security.SecurityElement]::Escape($node.InnerText)
        $keyEsc = [System.Security.SecurityElement]::Escape($key)
        [void]$sb.AppendLine("    <x:String x:Key=""$keyEsc"">$value</x:String>")
        $count++
    }
    [void]$sb.AppendLine('</ResourceDictionary>')
    if (-not $WhatIf)
    {
        $utf8 = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($dstPath, $sb.ToString(), $utf8)
    }
    return $count
}

$files = Get-ChildItem $SourceDir -Filter "*.xaml"
$total = 0
$failed = 0
foreach ($f in $files)
{
    # en_US is the blank Crowdin source-equal export; English comes from LocSource.
    if ($f.BaseName -eq "en_US") { continue }
    $dst = Join-Path $TargetDir ($f.BaseName + ".axaml")
    try
    {
        $n = ConvertOne $f.FullName $dst
        $total++
        "{0,-16} -> {1,-18} {2,5} keys" -f $f.Name, ($f.BaseName + ".axaml"), $n
    }
    catch
    {
        $failed++
        Write-Host ("FAILED {0}: {1}" -f $f.Name, $_.Exception.Message) -ForegroundColor Red
    }
}
"---- $total converted, $failed failed ----"
