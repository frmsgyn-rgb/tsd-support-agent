[Reading 27 lines from start (total: 27 lines, 0 remaining)]

param(
    [Parameter(Mandatory=$true)][string]$Path,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$Resolved = (Resolve-Path $Path).Path
$Info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Resolved)

if ($Info.ProductName -ne 'TSD Support Agent') {
    throw "Unexpected ProductName '$($Info.ProductName)' in $Resolved"
}

if ($Info.ProductVersion -ne $ExpectedVersion) {
    throw "Unexpected ProductVersion '$($Info.ProductVersion)' in $Resolved"
}

$ExpectedFileVersion = $ExpectedVersion + '.0'
if ($Info.FileVersion -ne $ExpectedFileVersion) {
    throw "Unexpected FileVersion '$($Info.FileVersion)' in $Resolved"
}

if ([string]::IsNullOrWhiteSpace($Info.FileDescription)) {
    throw "FileDescription is missing in $Resolved"
}

Write-Host "METADATA_OK path=$Resolved product=$($Info.ProductName) productVersion=$($Info.ProductVersion) fileVersion=$($Info.FileVersion)"
