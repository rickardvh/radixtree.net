param(
    [string]$Project = "benchmarks/Majako.Collections.RadixTree.Benchmarks/Majako.Collections.RadixTree.Benchmarks.fsproj",
    [string]$BaselinePath = "eng/perf-baseline.json",
    [int]$Repetitions = 3,
    [switch]$UseExistingResults,
    [string]$ResultsCsvPath = "BenchmarkDotNet.Artifacts/results/Majako.Collections.RadixTree.Benchmarks.ConcurrentBenchmarks-report.csv"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ScenarioKey {
    param(
        [string]$Method,
        [int]$ThreadCount,
        [int]$ContentionPercent
    )

    return "$Method|$ThreadCount|$ContentionPercent"
}

function Convert-ToMicroseconds {
    param([string]$RawValue)

    if ([string]::IsNullOrWhiteSpace($RawValue)) {
        throw "Missing benchmark timing value."
    }

    $trimmed = $RawValue.Trim().Replace('"', '')
    $match = [regex]::Match($trimmed, '^([0-9][0-9,\.]*)\s*([^\s]+)$')
    if (-not $match.Success) {
        throw "Unable to parse benchmark timing value '$RawValue'."
    }

    $number = [double]::Parse($match.Groups[1].Value.Replace(',', ''), [System.Globalization.CultureInfo]::InvariantCulture)
    $unit = $match.Groups[2].Value.ToLowerInvariant()
    $unit = $unit.Replace('Î¼', 'u').Replace('μ', 'u').Replace('µ', 'u')

    if ($unit -ne 'ns' -and $unit -ne 'us' -and $unit -ne 'ms' -and $unit -ne 's' -and $unit.EndsWith('s')) {
        $unit = 'us'
    }

    switch ($unit) {
        'ns' { return $number / 1000.0 }
        'us' { return $number }
        'ms' { return $number * 1000.0 }
        's' { return $number * 1000000.0 }
        default { throw "Unsupported timing unit '$unit' in '$RawValue'." }
    }
}

function Get-Median {
    param([double[]]$Values)

    if ($Values.Count -eq 0) {
        throw "Cannot compute median for an empty value set."
    }

    $sorted = @($Values | Sort-Object)
    $mid = [math]::Floor($sorted.Count / 2)

    if ($sorted.Count % 2 -eq 1) {
        return [double]$sorted[$mid]
    }

    return ([double]$sorted[$mid - 1] + [double]$sorted[$mid]) / 2.0
}

function Load-TrackedRows {
    param(
        [string]$CsvPath,
        [hashtable]$TrackedScenarios
    )

    if (-not (Test-Path $CsvPath)) {
        throw "Benchmark results file was not found: $CsvPath"
    }

    $rows = Import-Csv -Path $CsvPath -Delimiter ';'
    $trackedRows = @{}

    foreach ($row in $rows) {
        $method = $row.Method
        $threadCount = [int]$row.ThreadCount
        $contentionPercent = [int]$row.ContentionPercent
        $key = Get-ScenarioKey -Method $method -ThreadCount $threadCount -ContentionPercent $contentionPercent

        if (-not $TrackedScenarios.ContainsKey($key)) {
            continue
        }

        $rawTime = if ($row.PSObject.Properties.Name -contains 'Median' -and -not [string]::IsNullOrWhiteSpace($row.Median)) {
            $row.Median
        } else {
            $row.Mean
        }

        if ([string]::IsNullOrWhiteSpace($rawTime)) {
            throw "Neither Median nor Mean was available for scenario '$key'."
        }

        $trackedRows[$key] = Convert-ToMicroseconds -RawValue $rawTime
    }

    return $trackedRows
}

if (-not (Test-Path $BaselinePath)) {
    throw "Baseline file was not found: $BaselinePath"
}

$baseline = Get-Content $BaselinePath -Raw | ConvertFrom-Json
$catastrophicThreshold = [double]$baseline.catastrophicRegressionPercent
$warningThreshold = [double]$baseline.warningRegressionPercent

$scenarios = @($baseline.scenarios)
if ($scenarios.Count -eq 0) {
    throw "No baseline scenarios were defined in $BaselinePath."
}

$trackedScenarioIndex = @{}
foreach ($scenario in $scenarios) {
    $scenarioKey = Get-ScenarioKey -Method $scenario.method -ThreadCount $scenario.threadCount -ContentionPercent $scenario.contentionPercent
    $trackedScenarioIndex[$scenarioKey] = $scenario
}

$samples = @{}
for ($run = 1; $run -le $Repetitions; $run++) {
    if (-not $UseExistingResults) {
        Write-Host "Running benchmark repetition $run/$Repetitions..."
        dotnet run -c Release --project $Project -- --filter "*ConcurrentBenchmarks.ConcurrentRadixTree*Parallel*" --exporters csv | Out-Host
    } else {
        Write-Host "Using existing benchmark CSV for repetition $run/$Repetitions."
    }

    $runRows = Load-TrackedRows -CsvPath $ResultsCsvPath -TrackedScenarios $trackedScenarioIndex

    foreach ($scenario in $scenarios) {
        $key = Get-ScenarioKey -Method $scenario.method -ThreadCount $scenario.threadCount -ContentionPercent $scenario.contentionPercent
        if (-not $runRows.ContainsKey($key)) {
            throw "Missing tracked scenario '$key' in benchmark results."
        }

        if (-not $samples.ContainsKey($key)) {
            $samples[$key] = [System.Collections.Generic.List[double]]::new()
        }

        $samples[$key].Add([double]$runRows[$key])
    }
}

$comparisons = @()
$warnings = @()
$failures = @()

foreach ($scenario in $scenarios) {
    $key = Get-ScenarioKey -Method $scenario.method -ThreadCount $scenario.threadCount -ContentionPercent $scenario.contentionPercent
    $baselineUs = [double]$scenario.medianUs
    $currentUs = Get-Median -Values $samples[$key].ToArray()
    $regressionPercent = (($currentUs - $baselineUs) / $baselineUs) * 100.0

    $status =
        if ($regressionPercent -gt $catastrophicThreshold) {
            'FAIL'
        } elseif ($regressionPercent -gt $warningThreshold) {
            'WARN'
        } else {
            'PASS'
        }

    $comparison = [pscustomobject]@{
        Method = $scenario.method
        ThreadCount = $scenario.threadCount
        ContentionPercent = $scenario.contentionPercent
        BaselineUs = [math]::Round($baselineUs, 3)
        CurrentUs = [math]::Round($currentUs, 3)
        RegressionPercent = [math]::Round($regressionPercent, 2)
        Status = $status
    }

    $comparisons += $comparison

    if ($status -eq 'WARN') {
        $warnings += $comparison
    } elseif ($status -eq 'FAIL') {
        $failures += $comparison
    }
}

$summaryTable = $comparisons | Sort-Object Method, ThreadCount, ContentionPercent | Format-Table -AutoSize | Out-String

Write-Host $summaryTable

if ($warnings.Count -gt 0) {
    Write-Warning "Moderate regressions detected (>$warningThreshold% and <=$catastrophicThreshold%)."
}

if ($failures.Count -gt 0) {
    throw "Catastrophic regressions detected (>$catastrophicThreshold%)."
}
