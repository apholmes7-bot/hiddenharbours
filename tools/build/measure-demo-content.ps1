param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$evidence = [IO.Path]::GetFullPath((Join-Path $root 'Evidence~')) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($evidence, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Measurement output must be beneath the checkout Evidence~ directory.'
}

Push-Location $root
try {
    Add-Type -Path 'Assets/Tests/EditMode/AssetGuidIndex.cs', 'Assets/Tests/EditMode/DemoContentClosure.cs'
    $budget = [HiddenHarbours.Tests.EditMode.DemoContentClosure+Budget](
        Get-Content 'Assets/Tests/EditMode/Fixtures/DemoContentBudget.json' -Raw | ConvertFrom-Json)
    $index = [HiddenHarbours.Tests.EditMode.AssetGuidIndex]::Build([string[]]@('Assets', 'Packages'))
    $report = [HiddenHarbours.Tests.EditMode.DemoContentClosure]::Build($root, $budget, $index)
    $rows = @($report.Assets.Values)
    $violations = @([HiddenHarbours.Tests.EditMode.DemoContentClosure]::Violations($report, $budget))
    $summary = [ordered]@{
        reachableCount = $report.Parent.Count
        reachableBytes = $report.ReachableBytes
        unreachableCount = $report.Assets.Count - $report.Parent.Count
        unreachableBytes = $report.UnreachableBytes
        roots = @($report.Roots.GetEnumerator() | ForEach-Object { @{ path = $_.Key; reason = $_.Value } })
        reachableTop = @($rows | Where-Object { $report.Parent.ContainsKey($_.Path) } |
            Sort-Object Bytes -Descending | Select-Object -First 10 Path, Bytes, Pointer)
        unreachableTop = @($rows | Where-Object { -not $report.Parent.ContainsKey($_.Path) } |
            Sort-Object Bytes -Descending | Select-Object -First 10 Path, Bytes, Pointer)
        pointers = @($rows | Where-Object { $_.Pointer } | Select-Object Path, Bytes)
        unresolved = @($report.Unresolved)
        foreign = @($report.ForeignSceneReferences)
        violations = $violations
    }
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    [IO.File]::WriteAllText((Join-Path $output 'content-closure.tsv'),
        [HiddenHarbours.Tests.EditMode.DemoContentClosure]::Inventory($report))
    $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'content-closure.json') -Encoding UTF8
    Write-Output ('Reachable: {0} files, {1} bytes; unreachable: {2} files, {3} bytes; pointer sizes: {4}; violations: {5}.' -f
        $summary.reachableCount, $summary.reachableBytes, $summary.unreachableCount, $summary.unreachableBytes,
        $summary.pointers.Count, $violations.Count)
    if ($violations.Count -ne 0) { throw ($violations -join "`n") }
}
finally {
    Pop-Location
}
