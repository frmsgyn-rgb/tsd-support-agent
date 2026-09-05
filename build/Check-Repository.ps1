[Reading 42 lines from start (total: 42 lines, 0 remaining)]

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$ForbiddenExtensions = @('*.pfx','*.p12','*.key','*.snk','.env','.env.*')
foreach ($Pattern in $ForbiddenExtensions) {
    $Found = Get-ChildItem $Root -Recurse -File -Filter $Pattern -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|publish|artifacts|\.git)[\\/]' }
    if ($Found) { throw "Forbidden secret-like file found: $($Found.FullName -join ', ')" }
}

$TextFiles = Get-ChildItem $Root -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|publish|artifacts|\.git)[\\/]' -and
    $_.Name -ne 'Check-Repository.ps1' -and
    $_.Extension -in @('.cs','.csproj','.md','.yml','.yaml','.json','.ps1','.xml','.txt','')
}

$PrivateKeyMarker = 'BEGIN ' + 'PRIVATE KEY'
$RsaPrivateKeyMarker = 'BEGIN RSA ' + 'PRIVATE KEY'
$EcPrivateKeyMarker = 'BEGIN EC ' + 'PRIVATE KEY'
$OpenSshPrivateKeyMarker = 'BEGIN OPENSSH ' + 'PRIVATE KEY'

$Patterns = @(
    [regex]::Escape($PrivateKeyMarker),
    [regex]::Escape($RsaPrivateKeyMarker),
    [regex]::Escape($EcPrivateKeyMarker),
    [regex]::Escape($OpenSshPrivateKeyMarker),
    'Authorization:\s*Bearer\s+[A-Za-z0-9._~+/=-]{12,}',
    '(?i)api[_-]?key\s*[:=]\s*["''][^"'']{8,}',
    '(?i)password\s*[:=]\s*["''][^"'']{4,}',
    '(?i)secret\s*[:=]\s*["''][^"'']{8,}'
)

foreach ($File in $TextFiles) {
    $Content = Get-Content $File.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($Pattern in $Patterns) {
        if ($Content -match $Pattern) {
            throw "Potential secret pattern found in $($File.FullName)"
        }
    }
}

Write-Host 'REPOSITORY_CHECK_OK'
