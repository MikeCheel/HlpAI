# PowerShell script to run tests with coverage analysis
param(
    [string]$Configuration = "Debug",
    [int]$CoverageThreshold = 90
)

Write-Host "🧪 HlpAI - Test Suite with Coverage Analysis" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host

# Kill any running instances of the main application
Write-Host "🔄 Stopping any running application instances..." -ForegroundColor Yellow
Get-Process -Name "HlpAI" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Clean up previous builds
Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean src/HlpAI.sln --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Clean failed" -ForegroundColor Red
    exit 1
}

# Create output directories
Write-Host "📁 Creating output directories..." -ForegroundColor Yellow
$TestResultsDir = "./TestResults"
$CoverageDir = "$TestResultsDir/coverage"
New-Item -ItemType Directory -Force -Path $TestResultsDir | Out-Null
New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null

# Build the solution
Write-Host "🔨 Building solution..." -ForegroundColor Yellow
dotnet build src/HlpAI.sln --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

# Run tests with coverage
Write-Host "🧪 Running tests with coverage analysis..." -ForegroundColor Yellow
Write-Host "   Target Coverage: $CoverageThreshold%" -ForegroundColor Gray

$TestCommand = @(
    "test"
    "src/HlpAI.Tests/HlpAI.Tests.csproj"
    "--configuration", $Configuration
    "--collect", "XPlat Code Coverage"
    "--results-directory", $TestResultsDir
    "--logger", "trx"
    "--verbosity", "normal"
    "/p:CollectCoverage=true"
    "/p:CoverletOutputFormat=cobertura"
    "/p:CoverletOutput=$CoverageDir/"
    "/p:Include=[HlpAI]*"
    "/p:Exclude=[HlpAI]HlpAI.Program*%2c[*.Tests]*"
    "/p:Threshold=$CoverageThreshold"
    "/p:ThresholdType=line"
)

& dotnet @TestCommand

$TestExitCode = $LASTEXITCODE

# Find and process coverage file
Write-Host
Write-Host "📊 Processing coverage results..." -ForegroundColor Yellow

$CoverageFiles = Get-ChildItem -Path $TestResultsDir -Recurse -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending
if ($CoverageFiles.Count -eq 0) {
    Write-Host "⚠️  No coverage files found, looking for alternative formats..." -ForegroundColor Yellow
    $CoverageFiles = Get-ChildItem -Path $TestResultsDir -Recurse -Filter "*.cobertura.xml" | Sort-Object LastWriteTime -Descending
}

if ($CoverageFiles.Count -gt 0) {
    $LatestCoverage = $CoverageFiles[0]
    Write-Host "📋 Latest coverage file: $($LatestCoverage.FullName)" -ForegroundColor Gray
    
    # Generate HTML report if ReportGenerator is available
    try {
        Write-Host "📈 Generating HTML coverage report..." -ForegroundColor Yellow
        $ReportCommand = @(
            "tool", "run", "reportgenerator"
            "-reports:$($LatestCoverage.FullName)"
            "-targetdir:$CoverageDir/html"
            "-reporttypes:Html;HtmlInline_AzurePipelines;Cobertura;TextSummary"
            "-verbosity:Warning"
        )
        
        & dotnet @ReportCommand
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ HTML report generated: $CoverageDir/html/index.html" -ForegroundColor Green
        } else {
            Write-Host "⚠️  HTML report generation failed, trying to install ReportGenerator..." -ForegroundColor Yellow
            dotnet tool install --global dotnet-reportgenerator-globaltool
            & dotnet @ReportCommand
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ HTML report generated: $CoverageDir/html/index.html" -ForegroundColor Green
            }
        }
    } catch {
        Write-Host "⚠️  Could not generate HTML report: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    # Parse coverage percentage from XML
    try {
        [xml]$CoverageXml = Get-Content $LatestCoverage.FullName
        $LineCoverage = [math]::Round(([double]$CoverageXml.coverage.'line-rate') * 100, 2)
        $BranchCoverage = [math]::Round(([double]$CoverageXml.coverage.'branch-rate') * 100, 2)
        
        Write-Host
        Write-Host "📊 COVERAGE SUMMARY" -ForegroundColor Cyan
        Write-Host "===================" -ForegroundColor Cyan
        Write-Host "Line Coverage:   $LineCoverage%" -ForegroundColor $(if ($LineCoverage -ge $CoverageThreshold) { "Green" } else { "Red" })
        Write-Host "Branch Coverage: $BranchCoverage%" -ForegroundColor $(if ($BranchCoverage -ge 85) { "Green" } else { "Yellow" })
        Write-Host "Target:          $CoverageThreshold%" -ForegroundColor Gray
        Write-Host
        
        if ($LineCoverage -ge $CoverageThreshold) {
            Write-Host "🎉 Coverage target achieved!" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Coverage below target ($CoverageThreshold%)" -ForegroundColor Yellow
        }
        
    } catch {
        Write-Host "⚠️  Could not parse coverage data: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ No coverage files found" -ForegroundColor Red
}

# Show test results summary
Write-Host
Write-Host "📋 TEST SUMMARY" -ForegroundColor Cyan
Write-Host "===============" -ForegroundColor Cyan

$TrxFiles = Get-ChildItem -Path $TestResultsDir -Recurse -Filter "*.trx" | Sort-Object LastWriteTime -Descending
if ($TrxFiles.Count -gt 0) {
    try {
        [xml]$TrxXml = Get-Content $TrxFiles[0].FullName
        $TestResults = $TrxXml.TestRun.ResultSummary.Counters
        $Total = [int]$TestResults.total
        $Passed = [int]$TestResults.passed
        $Failed = [int]$TestResults.failed
        $Skipped = [int]$TestResults.inconclusive + [int]$TestResults.notExecuted
        
        Write-Host "Total Tests:  $Total" -ForegroundColor Gray
        Write-Host "Passed:       $Passed" -ForegroundColor Green
        Write-Host "Failed:       $Failed" -ForegroundColor $(if ($Failed -gt 0) { "Red" } else { "Green" })
        Write-Host "Skipped:      $Skipped" -ForegroundColor Yellow
        
    } catch {
        Write-Host "Could not parse test results" -ForegroundColor Yellow
    }
} else {
    Write-Host "No test result files found" -ForegroundColor Yellow
}

Write-Host
Write-Host "📁 Output Files:" -ForegroundColor Cyan
Write-Host "   Test Results: $TestResultsDir/" -ForegroundColor Gray
Write-Host "   Coverage:     $CoverageDir/" -ForegroundColor Gray
if (Test-Path "$CoverageDir/html/index.html") {
    Write-Host "   HTML Report:  $CoverageDir/html/index.html" -ForegroundColor Gray
}

Write-Host
if ($TestExitCode -eq 0) {
    Write-Host "✅ Test execution completed successfully!" -ForegroundColor Green
} else {
    Write-Host "❌ Test execution failed!" -ForegroundColor Red
}

exit $TestExitCode