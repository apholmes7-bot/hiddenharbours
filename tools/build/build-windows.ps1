#Requires -Version 5.1
<#
.SYNOPSIS
Build and privately zip a Windows Mono player under a seat-queued editor lease.
.EXAMPLE
./tools/build/build-windows.ps1 -SlotHome <live-keeper-folder> -LeaseLabel friend-build-f2 -Playtest
.NOTES
Use the pinned editor. Cold imports require an explicit opt-in and a sufficient lease.
Lz4HC and development trials live beside the release, each with the same package name.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SlotHome,
    [Parameter(Mandatory = $true)][string]$LeaseLabel,
    [string]$MainCommit,
    [string]$ProductName = 'Red Skies At Night',
    [ValidateSet('None', 'Lz4HC')][string]$Compression = 'None',
    [switch]$Development,
    [switch]$Playtest,
    [switch]$AllowColdImport,
    [switch]$ReuseBakedAssets,
    [switch]$PruneOldPackages,
    [ValidatePattern('^[a-z0-9-]+$')][string]$EvidenceName = 'friend-build',
    [ValidateRange(1, 90)][int]$WaitMinutes = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$env:GIT_NO_LAZY_FETCH = '1'
$env:GIT_OPTIONAL_LOCKS = '0'
$box = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildRoot = 'C:/hh-builds'
$slotScript = Join-Path $SlotHome 'slot.py'
$evidence = Join-Path $box ('Evidence~/' + $EvidenceName)
$utf8 = New-Object Text.UTF8Encoding($false)
$leaseId = $null
$player = $null
$launchIdentities = @{}

function Invoke-Git([string[]]$Arguments) {
    $lines = @(& git -C $box @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "git failed: $($Arguments -join ' ')" }
    return ($lines -join "`n").Trim()
}

function Assert-Disk {
    $drive = New-Object IO.DriveInfo('C:/')
    if ($drive.AvailableFreeSpace -lt 40000000000) { throw 'STOP: fewer than 40 GB free on the build drive.' }
}

function Invoke-Keeper([string[]]$Arguments, [int[]]$Allowed = @(0)) {
    Push-Location -LiteralPath $SlotHome
    try {
        $lines = @(& python -I -X utf8 $slotScript --json @Arguments)
        $code = $LASTEXITCODE
    }
    finally { Pop-Location }
    if ($lines.Count -lt 2) { throw 'STOP: keeper returned no state.' }
    $summary = [string]$lines[0]
    Write-Host $summary
    [IO.File]::AppendAllText($keeperLog, $summary + "`n", $utf8)
    $data = ($lines | Select-Object -Skip 1) -join "`n" | ConvertFrom-Json
    if ($code -notin $Allowed) { throw "STOP: keeper exit $code. $summary" }
    return [pscustomobject]@{ Code = $code; Data = $data; Summary = $summary }
}

function Identity-Lines {
    return (Get-Content -LiteralPath (Join-Path $box 'ProjectSettings/ProjectSettings.asset') |
        Where-Object { $_ -match '^  (companyName|productName|bundleVersion):' }) -join "`n"
}

function Restore-Settings {
    $changed = Invoke-Git @('diff', '--name-only', 'HEAD', '--', 'ProjectSettings/')
    if ($changed) {
        foreach ($path in ($changed -split "`n")) {
            $stat = Invoke-Git @('diff', '--numstat', 'HEAD', '--', $path)
            [IO.File]::AppendAllText($keeperLog, "Restore by name: $stat`n", $utf8)
            $null = Invoke-Git @('checkout', '--', $path)
        }
    }
}

function Test-ProcessIdentity($Expected, $Current) {
    if (!$Current) { return $false }
    if (!$Expected -or !$Expected.name -or !$Expected.created) {
        throw 'STOP: a launched process identity is unknown.'
    }
    return $Current.name -ieq $Expected.name -and
        [Math]::Abs([double]$Current.created - [double]$Expected.created) -le 1
}

function Remember-Launch($Data) {
    $launch = $Data.state.lease.launch
    if (!$launch) { return }
    if ($launch.editor) {
        $launchIdentities[[int]$launch.editor.pid] = [pscustomobject]@{
            name = 'Unity.exe'; created = $launch.editor.created_ms
        }
    }
    foreach ($child in $launch.children) {
        $launchIdentities[[int]$child.pid] = $child
    }
}

function Invoke-Editor([string[]]$UnityArguments) {
    $run = Invoke-Keeper (@('launch', '--lease', $leaseId, '--max-minutes', '1', '--') + $UnityArguments) @(0, 3)
    Remember-Launch $run.Data
    while ($run.Code -eq 3) {
        $run = Invoke-Keeper @('wait-run', '--lease', $leaseId, '--max-minutes', '1') @(0, 3)
        Remember-Launch $run.Data
        Assert-Disk
    }
    $result = $run.Data.result
    [IO.File]::AppendAllText($keeperLog, "Editor CLOSED PID: $($result.editor_pid); child PIDs: " +
        ($result.child_pids -join ', ') + "; end: $($result.end)`n", $utf8)
    if ($result.exit_code -ne 0 -or $result.how -ne 'exited') { throw 'STOP: editor did not exit cleanly.' }
}

function Return-Lease {
    $returnPath = Join-Path $SlotHome ("returns/$leaseId.json")
    if (Test-Path -LiteralPath $returnPath) {
        # The watchdog may already have returned an expired lease.
        $record = Get-Content -LiteralPath $returnPath -Raw | ConvertFrom-Json
        Write-Host $record.summary
    }
    else {
        $returned = Invoke-Keeper @('give', '--lease', $leaseId, '--stop')
        $record = $returned.Data.result
    }
    [IO.File]::AppendAllText($keeperLog, "Editor closed; launched PIDs: " +
        ($record.launched_pids -join ', ') + "; all gone: $($record.all_launched_pids_gone)`n", $utf8)
    if (!$record.all_launched_pids_gone) {
        # A PID can already belong to an unrelated process by the return probe.
        # Never stop that process or treat the number alone as a surviving editor.
        foreach ($launch in $record.launches) {
            foreach ($child in $launch.children) { $launchIdentities[[int]$child.pid] = $child }
        }
        foreach ($processId in $record.launched_pids_alive) {
            $expected = $launchIdentities[[int]$processId]
            if (!$expected) { throw 'STOP: cannot verify a reported live process identity.' }
            $process = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction Stop
            $current = $null
            if ($process) {
                $current = [pscustomobject]@{
                    name = $process.Name
                    created = ([DateTimeOffset]$process.CreationDate).ToUnixTimeMilliseconds()
                }
            }
            if (Test-ProcessIdentity $expected $current) { throw 'STOP: the editor PID tree is still alive.' }
            [IO.File]::AppendAllText($keeperLog, "CLOSED identity for PID $processId; absent or reused.`n", $utf8)
        }
    }
}

function Remove-OldPackages {
    # Only completed packages made by this script qualify. Refuse links before recursive deletion.
    $candidates = @(Get-ChildItem -LiteralPath $buildRoot -Directory |
        Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}-[0-9a-f]{8}$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'package-info.json')) } |
        Sort-Object { (Get-Item -LiteralPath (Join-Path $_.FullName 'package-info.json')).LastWriteTimeUtc } -Descending)
    foreach ($candidate in ($candidates | Select-Object -Skip 3)) {
        $target = [IO.Path]::GetFullPath($candidate.FullName)
        $root = [IO.Path]::GetFullPath($buildRoot).TrimEnd('\', '/')
        if ([IO.Path]::GetDirectoryName($target) -ne $root) { throw 'Unsafe retention target.' }
        $manifest = Get-Content -LiteralPath (Join-Path $target 'package-info.json') -Raw | ConvertFrom-Json
        if ($manifest.generator -ne 'windows-player-package-v1' -or $manifest.buildId -ne $candidate.Name) {
            continue
        }
        $pending = New-Object 'Collections.Generic.Stack[string]'
        $pending.Push($target)
        while ($pending.Count -gt 0) {
            $directory = Get-Item -LiteralPath $pending.Pop() -Force
            if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Retention refuses links.' }
            foreach ($entry in (Get-ChildItem -LiteralPath $directory.FullName -Force)) {
                if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Retention refuses links.' }
                if ($entry.PSIsContainer) { $pending.Push($entry.FullName) }
            }
        }
        Write-Host "Removing completed older package: $target"
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

Push-Location -LiteralPath $box
try {
    if (!(Test-Path -LiteralPath $slotScript -PathType Leaf)) { throw 'The live slot.py was not found.' }
    $SlotHome = (Resolve-Path -LiteralPath $SlotHome).Path
    $slotScript = Join-Path $SlotHome 'slot.py'
    if (!$AllowColdImport -and !(Test-Path -LiteralPath (Join-Path $box 'Library/ArtifactDB'))) {
        throw 'STOP: a warm Library is required; do not start a cold import.'
    }
    $null = Get-Command git, python, node -ErrorAction Stop
    if (Invoke-Git @('status', '--porcelain', '--', '.', ':(exclude)Evidence~')) {
        throw 'Commit or restore named changes before building.'
    }
    $commit = Invoke-Git @('rev-parse', 'HEAD')
    if (!$MainCommit) { $MainCommit = Invoke-Git @('rev-parse', 'origin/main') }
    $MainCommit = Invoke-Git @('rev-parse', '--verify', ($MainCommit + '^{commit}'))
    $null = Invoke-Git @('merge-base', '--is-ancestor', $MainCommit, $commit)
    if ($commit -notmatch '^[0-9a-f]{40}$') { throw 'A full source commit is required.' }
    if ([string]::IsNullOrWhiteSpace($ProductName) -or $ProductName -match '[\x00-\x1f<>:"/\\|?*]' -or
        $ProductName -match '[. ]$') { throw 'Invalid product file name.' }
    Assert-Disk
    $buildId = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd') + '-' + $commit.Substring(0, 8)
    $packageName = 'red-skies-at-night-' + $buildId
    $buildHome = Join-Path $buildRoot $buildId
    $variantHome = $buildHome
    if ($Development) { $variantHome = Join-Path $variantHome 'development' }
    if ($Compression -eq 'Lz4HC') { $variantHome = Join-Path $variantHome 'lz4hc' }
    $output = Join-Path $variantHome $packageName
    $zip = Join-Path $variantHome ($packageName + '.zip')
    if ((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $zip)) {
        throw 'STOP: this output already exists; preserve it and report before another attempt.'
    }
    $null = New-Item -ItemType Directory -Path $buildRoot -Force
    if ((Get-Item -LiteralPath $buildRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'STOP: the build root must be a real directory.'
    }
    $parent = $variantHome
    while ($parent -ne [IO.Path]::GetFullPath($buildRoot)) {
        if ((Test-Path -LiteralPath $parent) -and
            ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'STOP: a package parent must not be a link.'
        }
        $parent = [IO.Path]::GetDirectoryName($parent)
    }
    $null = New-Item -ItemType Directory -Path $variantHome, $evidence -Force
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $keeperLog = Join-Path $evidence ("keeper-$stamp.txt")
    $unityLog = Join-Path $evidence ("build-$stamp.log")
    $roadLog = Join-Path $evidence ("roads-$stamp.log")
    $identityBefore = Identity-Lines
    $grant = Invoke-Keeper @('wait', '--label', $LeaseLabel, '--max-minutes', "$WaitMinutes") @(0, 3)
    if ($grant.Code -eq 3) { throw 'READY: no lease granted within the bounded wait.' }
    $leaseId = $grant.Data.result.lease
    if (!$leaseId) { throw 'STOP: the grant has no lease id.' }
    if ([IO.Path]::GetFullPath($grant.Data.state.lease.box).TrimEnd('\', '/') -ne $box.TrimEnd('\', '/')) {
        throw 'STOP: the lease names a different box.'
    }
    try {
        Assert-Disk
        if (!(Test-Path -LiteralPath (Join-Path $box 'Library/ArtifactDB'))) {
            $importLog = Join-Path $evidence ("import-$stamp.log")
            Invoke-Editor @('-batchmode', '-quit', '-projectPath', $box, '-buildTarget', 'Win64', '-logFile', $importLog)
            if (!(Test-Path -LiteralPath (Join-Path $box 'Library/ArtifactDB'))) {
                throw 'STOP: cold import did not produce the artifact database.'
            }
            Restore-Settings
        }
        $roads = Join-Path $box 'Assets/StreamingAssets/RoadFloor/NineMileCreek'
        if (!$ReuseBakedAssets) {
            & node (Join-Path $box 'tools/nmc-roads/bake-chunks.cjs') $roads *> $roadLog
            if ($LASTEXITCODE -ne 0) { throw "STOP: road bake failed; see $roadLog" }
        }
        $null = Invoke-Keeper @('keep', '--lease', $leaseId)
        Assert-Disk
        $unityArgs = @('-batchmode', '-quit', '-projectPath', $box, '-buildTarget', 'Win64',
            '-executeMethod', 'HiddenHarbours.Tools.Editor.PlayerBuild.BuildWindows',
            '-buildOutput', $output, '-buildProduct', $ProductName, '-buildCommit', $commit,
            '-buildMainCommit', $MainCommit, '-buildCompression', $Compression, '-logFile', $unityLog)
        if ($Development) { $unityArgs += '-buildDevelopment' }
        if ($ReuseBakedAssets) { $unityArgs += '-buildReuseBakedAssets' }
        Invoke-Editor $unityArgs
        if ((Identity-Lines) -ne $identityBefore) { throw 'STOP: a build name stuck in ProjectSettings.' }
        Restore-Settings
        $exe = Join-Path $output ($ProductName + '.exe')
        $info = Join-Path $output 'build-info.txt'
        if (!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $info)) {
            throw 'STOP: missing executable or completion stamp; executeMethod may not have run.'
        }
        if ((Get-Content -LiteralPath $info) -notcontains "source-commit: $commit") {
            throw 'STOP: build stamp does not match the source commit.'
        }
        if ($Playtest) {
            if ($Development) { throw 'Use the release player for the install check.' }
            $playerLog = Join-Path $evidence ("player-$stamp.log")
            $playerArgs = '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "' + $playerLog + '"'
            $player = Start-Process -FilePath $exe -ArgumentList $playerArgs -WorkingDirectory $output -PassThru
            Write-Host "Player PID $($player.Id); close the player after the opening and St Peters check. Log: $playerLog"
            while (!$player.WaitForExit(30000)) { $null = Invoke-Keeper @('keep', '--lease', $leaseId) }
            if ($player.ExitCode -ne 0) { throw "STOP: player exited $($player.ExitCode)." }
        }
    }
    finally {
        if ($player -and !$player.HasExited) {
            $null = $player.CloseMainWindow()
            if (!$player.WaitForExit(10000)) { $player.Kill(); $player.WaitForExit() }
        }
        # --stop only closes this keeper lease's checked PID tree, never editors by name.
        Return-Lease
        $leaseId = $null
        Restore-Settings
    }

    # No slot is held for packaging. Keep logs in evidence, outside the private zip.
    Assert-Disk
    $readme = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'README.txt')).Replace('Red Skies At Night', $ProductName)
    [IO.File]::WriteAllText((Join-Path $output 'README.txt'), $readme, $utf8)
    $card = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CARD.txt')).Replace('Red Skies At Night', $ProductName)
    [IO.File]::WriteAllText((Join-Path $output 'CARD.txt'), $card, $utf8)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($output, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
    $folderBytes = (Get-ChildItem -LiteralPath $output -File -Recurse | Measure-Object Length -Sum).Sum
    $zipBytes = (Get-Item -LiteralPath $zip).Length
    $sizes = "folder-bytes: $folderBytes`nzip-bytes: $zipBytes`ncompression: $Compression`n"
    [IO.File]::WriteAllText((Join-Path $variantHome 'size.txt'), $sizes, $utf8)
    Write-Host "Folder: $output ($folderBytes bytes)"
    Write-Host "Zip: $zip ($zipBytes bytes)"
    if ($zipBytes -gt 4000000000) { throw 'STOP: zip exceeds 4 GB; report its path and size.' }
    $manifest = @{ generator = 'windows-player-package-v1'; buildId = $buildId; sourceCommit = $commit } |
        ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $buildHome 'package-info.json'), $manifest, $utf8)
    if ($PruneOldPackages) { Remove-OldPackages }
}
finally {
    if ($leaseId) { Return-Lease }
    Pop-Location
}
