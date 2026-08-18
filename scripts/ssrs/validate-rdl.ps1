[CmdletBinding()]
param(
    [string]$ReportDirectory = (Join-Path $PSScriptRoot '../../reports')
)

$ErrorActionPreference = 'Stop'
$resolvedDirectory = Resolve-Path $ReportDirectory
$files = Get-ChildItem -Path $resolvedDirectory -Filter '*.rdl' -File

if ($files.Count -eq 0) {
    throw "No RDL files found under $resolvedDirectory"
}

foreach ($file in $files) {
    try {
        [xml](Get-Content -Raw -Path $file.FullName) | Out-Null
        Write-Host "Validated $($file.FullName)"
    }
    catch {
        throw "Invalid SSRS XML in $($file.FullName): $($_.Exception.Message)"
    }
}

Write-Host "Validated $($files.Count) SSRS report definition(s)."
