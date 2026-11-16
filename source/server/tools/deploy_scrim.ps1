# Puts the server build of this repository into a game folder, with a backup (docs/PVP-8V8-ROADMAP.md, M0.1).
#
#   .\deploy_scrim.ps1                      what would change (nothing is written)
#   .\deploy_scrim.ps1 -Apply               run the tests, back up, install
#   .\deploy_scrim.ps1 -Rollback <folder>   put a backup made by this script back
#
# -Runtime names the game's runtime folder; the default is the scrim copy. A folder that has been played
# (it has an account.txt) is refused unless -AllowPlayed is given. Build first:
#   dotnet build "source/server/Dawn of Light.sln" -c Release
# Only GameServer.dll and its .pdb are replaced, in server, server\lib and server\win-x64 (the rule in AGENTS.md).
# CoreBase, CoreDatabase and CoreServer have not changed since the 0.37 release, and the game's win-x64 copies of
# them are a different build flavour, so they stay. If their source ever changes, name them with -Assemblies.
# Works in Windows PowerShell 5.1.
param(
    [string]$Runtime = 'D:\OfflineDAoC-scrim\runtime',
    [string[]]$Assemblies = @('GameServer'),
    [switch]$Apply,
    [switch]$SkipTests,
    [switch]$AllowPlayed,
    [string]$Rollback = ''
)
$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path $PSScriptRoot -Parent
$release = Join-Path $serverRoot 'Release'

function Assert-Stopped {
    # The launcher may stay open: it does not hold the server files. The server and the game client do.
    if (Get-Process CoreServer, game, camelot, connect -ErrorAction SilentlyContinue) {
        throw 'Close the server and the game client first.'
    }
}
function Get-Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }

$Runtime = [IO.Path]::GetFullPath($Runtime)
if (!(Test-Path -LiteralPath (Join-Path $Runtime 'server\GameServer.dll') -PathType Leaf)) {
    throw "No game server was found in $Runtime (expected server\GameServer.dll)."
}
Assert-Stopped

if ($Rollback) {
    $backup = [IO.Path]::GetFullPath($Rollback)
    $manifest = Get-Content -LiteralPath (Join-Path $backup 'manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.Runtime -ne $Runtime) { throw "That backup belongs to $($manifest.Runtime), not $Runtime." }
    foreach ($entry in $manifest.Entries) {
        $saved = Join-Path $backup $entry.Relative
        if ((Get-Hash $saved) -ne $entry.OldHash) { throw "The backup of $($entry.Relative) is damaged; nothing more was restored." }
        Copy-Item -LiteralPath $saved -Destination (Join-Path $Runtime $entry.Relative) -Force
    }
    Write-Output "Restored $($manifest.Entries.Count) files from $backup."
    return
}

# The scrim copy gets an account.txt as soon as its launcher is opened; only other folders are protected.
if (!$AllowPlayed -and $Runtime -notmatch 'scrim' -and (Test-Path -LiteralPath (Join-Path $Runtime 'account.txt'))) {
    throw "$Runtime has an account.txt: it is a played game. Use the scrim copy, or pass -AllowPlayed."
}

$entries = foreach ($name in $Assemblies) {
    foreach ($extension in '.dll', '.pdb') {
        $file = $name + $extension
        $source = Join-Path $release ('lib\' + $file)
        if (!(Test-Path -LiteralPath $source)) { $source = Join-Path $release $file }
        if (!(Test-Path -LiteralPath $source)) { continue }
        foreach ($folder in 'server', 'server\lib', 'server\win-x64') {
            $relative = Join-Path $folder $file
            $target = Join-Path $Runtime $relative
            if (!(Test-Path -LiteralPath $target -PathType Leaf)) { continue }
            [pscustomobject]@{
                Relative = $relative; Source = $source
                OldHash = Get-Hash $target; NewHash = Get-Hash $source
            }
        }
    }
}
$entries = @($entries | Where-Object { $_.OldHash -ne $_.NewHash })
if ($entries.Count -eq 0) { Write-Output "Nothing to do: $Runtime already has this build."; return }
if (!$Apply) {
    $entries | Format-Table Relative, @{ n = 'Installed'; e = { $_.OldHash.Substring(0, 12) } }, @{ n = 'Build'; e = { $_.NewHash.Substring(0, 12) } }
    Write-Output "$($entries.Count) files would be replaced in $Runtime. Run again with -Apply."
    return
}

if (!$SkipTests) {
    & dotnet test (Join-Path $serverRoot 'Tests\Tests.csproj') -c Release --no-build -v q -nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'The server tests fail (or the test project is not built). Nothing was installed.' }
    # The tests must have run against the build that is about to be installed.
    $tested = Join-Path $serverRoot 'build\Tests\Release\lib\GameServer.dll'
    if (!(Test-Path -LiteralPath $tested) -or (Get-Hash $tested) -ne (Get-Hash (Join-Path $release 'lib\GameServer.dll'))) {
        throw 'The tested GameServer.dll is not the one in Release\lib. Build the solution again, then retry.'
    }
}

$backup = Join-Path (Split-Path $Runtime -Parent) ('deployment-backups\scrim-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach ($entry in $entries) {
    $saved = Join-Path $backup $entry.Relative
    New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $Runtime $entry.Relative) -Destination $saved
    if ((Get-Hash $saved) -ne $entry.OldHash) { throw 'A backup copy does not match its original. Nothing was installed.' }
}
@{ Runtime = $Runtime; Created = (Get-Date -Format o); Entries = $entries } | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $backup 'manifest.json') -Encoding UTF8

try {
    foreach ($entry in $entries) {
        Assert-Stopped
        $target = Join-Path $Runtime $entry.Relative
        Copy-Item -LiteralPath $entry.Source -Destination $target -Force
        if ((Get-Hash $target) -ne $entry.NewHash) { throw "$($entry.Relative) did not install correctly." }
    }
} catch {
    foreach ($entry in $entries) {
        Copy-Item -LiteralPath (Join-Path $backup $entry.Relative) -Destination (Join-Path $Runtime $entry.Relative) -Force
    }
    throw
}
Write-Output "Installed $($entries.Count) files into $Runtime. Backup: $backup"
Write-Output "To undo: .\deploy_scrim.ps1 -Runtime `"$Runtime`" -Rollback `"$backup`""
