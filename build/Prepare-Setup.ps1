[Reading 46 lines from start (total: 46 lines, 0 remaining)]

param(
    [Parameter(Mandatory=$true)][string]$AgentPath,
    [string]$Configuration = 'Release',
    [string]$Output = 'artifacts/setup-unsigned'
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Agent = (Resolve-Path $AgentPath).Path
$AgentProject = Join-Path $Root 'src\TsdSupportAgent\TsdSupportAgent.csproj'
$SetupProject = Join-Path $Root 'src\TsdSupportSetup\TsdSupportSetup.csproj'
$Generated = Join-Path $Root 'src\TsdSupportSetup\EmbeddedAgentInfo.Generated.cs'
$Out = Join-Path $Root $Output

[xml]$ProjectXml = Get-Content $AgentProject
$Version = [string]$ProjectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($Version)) { throw 'Agent version not found.' }

$Hash = (Get-FileHash $Agent -Algorithm SHA256).Hash.ToLowerInvariant()
$GeneratedText = @"
static class EmbeddedAgentInfo
{
    public const string Version = `"$Version`";
    public const string Sha256 = `"$Hash`";
}
"@
Set-Content -Path $Generated -Value $GeneratedText -Encoding UTF8

Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Out | Out-Null

dotnet restore $SetupProject --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Setup restore failed.' }

dotnet publish $SetupProject -c $Configuration -o $Out --no-restore -p:EmbeddedAgentPath=$Agent
if ($LASTEXITCODE -ne 0) { throw 'Setup build failed.' }

$SetupExe = Join-Path $Out 'TSD-Support-Setup.exe'
if (-not (Test-Path $SetupExe)) { throw 'Setup executable not found.' }

& (Join-Path $PSScriptRoot 'Validate-BinaryMetadata.ps1') -Path $SetupExe -ExpectedVersion $Version

$SetupHash = (Get-FileHash $SetupExe -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "EMBEDDED_AGENT_SHA256=$Hash"
Write-Host "SETUP_PATH=$SetupExe"
Write-Host "SETUP_SHA256=$SetupHash"
