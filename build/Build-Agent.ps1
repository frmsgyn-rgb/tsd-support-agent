param(
    [string]$Configuration = 'Release',
    [string]$Output = 'artifacts/agent-unsigned'
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Project = Join-Path $Root 'src\TsdSupportAgent\TsdSupportAgent.csproj'
$Out = Join-Path $Root $Output

Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Out | Out-Null

dotnet restore $Project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Agent restore failed.' }

dotnet publish $Project -c $Configuration -o $Out --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Agent build failed.' }

$Exe = Join-Path $Out 'TsdSupportAgent.exe'
if (-not (Test-Path $Exe)) { throw 'Agent executable not found.' }

& (Join-Path $PSScriptRoot 'Validate-BinaryMetadata.ps1') -Path $Exe -ExpectedVersion '0.4.0'

$Hash = (Get-FileHash $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
$Size = (Get-Item $Exe).Length
Write-Host "AGENT_PATH=$Exe"
Write-Host "AGENT_SIZE=$Size"
Write-Host "AGENT_SHA256=$Hash"
