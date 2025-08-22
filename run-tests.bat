@echo off
echo 🧪 HlpAI - Test Suite
echo =====================================
echo.

REM Kill any running instances
echo 🔄 Stopping running application instances...
taskkill /IM "HlpAI.exe" /F >nul 2>&1
timeout /t 2 >nul

REM Clean and build
echo 🧹 Cleaning previous builds...
dotnet clean --verbosity quiet
if %ERRORLEVEL% neq 0 (
    echo ❌ Clean failed
    exit /b 1
)

echo 🔨 Building solution...
dotnet build --verbosity quiet
if %ERRORLEVEL% neq 0 (
    echo ❌ Build failed
    exit /b 1
)

REM Create directories
echo 📁 Creating test output directories...
if not exist TestResults mkdir TestResults
if not exist TestResults\coverage mkdir TestResults\coverage

REM Run tests with coverage
echo 🧪 Running tests with coverage...
dotnet test src/HlpAI.Tests/HlpAI.Tests.csproj ^
    --collect "XPlat Code Coverage" ^
    --results-directory TestResults ^
    --logger trx ^
    --verbosity normal ^
    /p:CollectCoverage=true ^
    /p:CoverletOutputFormat=cobertura ^
    /p:CoverletOutput=TestResults/coverage/ ^
    /p:Include="[HlpAI]*" ^
    /p:Exclude="[HlpAI]HlpAI.Program*,[*.Tests]*"

set TEST_EXIT_CODE=%ERRORLEVEL%

echo.
if %TEST_EXIT_CODE% equ 0 (
    echo ✅ Tests completed successfully!
) else (
    echo ❌ Tests failed!
)

echo.
echo 📁 Output files:
echo    Test Results: TestResults/
echo    Coverage:     TestResults/coverage/

exit /b %TEST_EXIT_CODE%