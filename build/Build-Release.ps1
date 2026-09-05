[Reading 19 lines from start (total: 19 lines, 0 remaining)]

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

& (Join-Path $PSScriptRoot 'Build-Agent.ps1')

$Agent = Join-Path $Root 'artifacts\agent-unsigned\TsdSupportAgent.exe'
& (Join-Path $PSScriptRoot 'Prepare-Setup.ps1') -AgentPath $Agent

$Setup = Join-Path $Root 'artifacts\setup-unsigned\TSD-Support-Setup.exe'
$HashFile = Join-Path $Root 'artifacts\SHA256SUMS.txt'
$AgentHash = (Get-FileHash $Agent -Algorithm SHA256).Hash.ToLowerInvariant()
$SetupHash = (Get-FileHash $Setup -Algorithm SHA256).Hash.ToLowerInvariant()
@(
    "$AgentHash  TsdSupportAgent.exe",
    "$SetupHash  TSD-Support-Setup.exe"
) | Set-Content -Path $HashFile -Encoding ASCII

Write-Host 'BUILD_RELEASE_OK'
Get-Content $HashFile
