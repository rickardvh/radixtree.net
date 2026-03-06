param(
    [string]$Configuration = "Release",
    [int]$StressRuns = 3,
    [int]$BenchmarkRepetitions = 3,
    [int]$CorrectnessTimeoutSeconds = 300,
    [int]$StressTimeoutSeconds = 300,
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

function Invoke-CommandWithTimeout {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList,
        [int]$TimeoutSeconds,
        [string]$CommandDescription
    )

    Write-Host "$CommandDescription"
    Write-Host "> $FilePath $($ArgumentList -join ' ')"

    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()
    $workingDirectory = (Get-Location).Path

    $job = Start-Job -ScriptBlock {
        param($wd, $file, $commandArgs, $stdout, $stderr)

        Set-Location $wd
        & $file @commandArgs 1> $stdout 2> $stderr
        return [int]$LASTEXITCODE
    } -ArgumentList $workingDirectory, $FilePath, $ArgumentList, $stdoutPath, $stderrPath

    if (-not (Wait-Job -Job $job -Timeout $TimeoutSeconds)) {
        Write-Warning "$CommandDescription timed out after $TimeoutSeconds seconds. Terminating process tree."
        Stop-Job -Job $job -Force
        Receive-Job -Job $job -ErrorAction SilentlyContinue | Out-Null
        if (Test-Path $stdoutPath) { Get-Content $stdoutPath | Write-Host }
        if (Test-Path $stderrPath) { Get-Content $stderrPath | Write-Host }
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        Remove-Item $stdoutPath, $stderrPath -ErrorAction SilentlyContinue
        throw "$CommandDescription timed out after $TimeoutSeconds seconds."
    }

    $jobOutput = Receive-Job -Job $job
    $exitCode = [int]($jobOutput | Select-Object -Last 1)

    if (Test-Path $stdoutPath) { Get-Content $stdoutPath | Write-Host }
    if (Test-Path $stderrPath) { Get-Content $stderrPath | Write-Host }
    Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    Remove-Item $stdoutPath, $stderrPath -ErrorAction SilentlyContinue

    if ($exitCode -ne 0) {
        throw "$CommandDescription failed with exit code $exitCode."
    }
}

function Assert-NoVulnerablePackages {
    $json = dotnet package list --project radixtree.net.sln --vulnerable --include-transitive --format json --output-version 1
    Assert-LastExitCode -CommandDescription "dotnet package list --vulnerable"

    $report = $json | ConvertFrom-Json
    $vulnerablePackages = [System.Collections.Generic.List[string]]::new()

    foreach ($project in @($report.projects)) {
        if (-not ($project.PSObject.Properties.Name -contains 'frameworks')) {
            continue
        }

        foreach ($framework in @($project.frameworks)) {
            foreach ($packageSetName in @('topLevelPackages', 'transitivePackages')) {
                if (-not ($framework.PSObject.Properties.Name -contains $packageSetName)) {
                    continue
                }

                foreach ($package in @($framework.$packageSetName)) {
                    if (-not ($package.PSObject.Properties.Name -contains 'vulnerabilities')) {
                        continue
                    }

                    if (-not @($package.vulnerabilities).Count) {
                        continue
                    }

                    $projectPath = $project.path
                    $frameworkName = $framework.framework
                    $packageName = $package.id
                    $resolvedVersion = $package.resolvedVersion
                    $vulnerablePackages.Add("$projectPath [$frameworkName] $packageName $resolvedVersion")
                }
            }
        }
    }

    if ($vulnerablePackages.Count -gt 0) {
        throw "Package vulnerability check failed.`n$($vulnerablePackages -join [Environment]::NewLine)"
    }
}

Invoke-Stage -Name "Stage A: Correctness tests" -Action {
    Invoke-CommandWithTimeout -FilePath "dotnet" `
        -ArgumentList @("test", "--solution", "radixtree.net.sln", "-c", $Configuration, "--", "--filter-not-trait", "Category=Stress") `
        -TimeoutSeconds $CorrectnessTimeoutSeconds `
        -CommandDescription "Stage A correctness test run"
}

Invoke-Stage -Name "Stage B: Thread-safety stress tests" -Action {
    for ($run = 1; $run -le $StressRuns; $run++) {
        Write-Host "Stress run $run/$StressRuns"
        Invoke-CommandWithTimeout -FilePath "dotnet" `
            -ArgumentList @("test", "--solution", "radixtree.net.sln", "-c", $Configuration, "--", "--filter-trait", "Category=Stress") `
            -TimeoutSeconds $StressTimeoutSeconds `
            -CommandDescription "Stage B stress run $run"
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
    Assert-NoVulnerablePackages

    dotnet build radixtree.net.sln -c $Configuration -t:Rebuild /p:TreatWarningsAsErrors=true /p:CodeAnalysisTreatWarningsAsErrors=false /check
    Assert-LastExitCode -CommandDescription "Stage D dotnet build"

    $markerHits = Get-ChildItem src, tests, benchmarks, eng -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj|BenchmarkDotNet\.Artifacts|TestResults)\\' } | Select-String -Pattern 'TODO|FIXME|HACK' | Where-Object { $_.Path -notlike '*eng\verify-priorities.ps1' }

    if ($markerHits) {
        $formatted = $markerHits | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }
        throw "Readability marker check failed.`n$($formatted -join [Environment]::NewLine)"
    }
}

Write-Host ""
Write-Host "Priority verification completed successfully." -ForegroundColor Green
