param(
    [string]$Configuration = "Release",
    [int]$StressRuns = 3,
    [int]$BenchmarkRepetitions = 3,
    [switch]$SkipPerformance
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Stage {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    & $Action
    Write-Host "=== $Name completed ===" -ForegroundColor Green
}

function Assert-LastExitCode {
    param(
        [string]$CommandDescription
    )

    if ($LASTEXITCODE -ne 0) {
        throw "$CommandDescription failed with exit code $LASTEXITCODE."
    }
}

Invoke-Stage -Name "Stage A: Correctness tests" -Action {
    dotnet test radixtree.net.sln -c $Configuration --filter "Category!=Stress" --blame-hang --blame-hang-timeout 180s
    Assert-LastExitCode -CommandDescription "Stage A dotnet test"
}

Invoke-Stage -Name "Stage B: Thread-safety stress tests" -Action {
    for ($run = 1; $run -le $StressRuns; $run++) {
        Write-Host "Stress run $run/$StressRuns"
        dotnet test radixtree.net.sln -c $Configuration --filter "Category=Stress" --blame-hang --blame-hang-timeout 180s
        Assert-LastExitCode -CommandDescription "Stage B stress run $run dotnet test"
    }
}

if (-not $SkipPerformance) {
    Invoke-Stage -Name "Stage C: Performance regression checks" -Action {
        & "$PSScriptRoot/compare-priority-benchmarks.ps1" -Repetitions $BenchmarkRepetitions
        Assert-LastExitCode -CommandDescription "Stage C benchmark comparison"
    }
} else {
    Write-Host ""
    Write-Host "=== Stage C: Performance regression checks skipped ===" -ForegroundColor Yellow
}

Invoke-Stage -Name "Stage D: Readability and brevity static checks" -Action {
    dotnet build radixtree.net.sln -c $Configuration -warnaserror
    Assert-LastExitCode -CommandDescription "Stage D dotnet build"

    $markerHits = Get-ChildItem src, tests, benchmarks, eng -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj|BenchmarkDotNet\.Artifacts|TestResults)\\' } | Select-String -Pattern 'TODO|FIXME|HACK' | Where-Object { $_.Path -notlike '*eng\verify-priorities.ps1' }

    if ($markerHits) {
        $formatted = $markerHits | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }
        throw "Readability marker check failed.`n$($formatted -join [Environment]::NewLine)"
    }
}

Write-Host ""
Write-Host "Priority verification completed successfully." -ForegroundColor Green
