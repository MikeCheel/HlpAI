@echo off
echo.
echo HlpAI File Extractor Test Script
echo =================================
echo.

set TEST_DIR=%~dp0
set APP_DIR=%TEST_DIR%\..\src\HlpAI

echo Test documents location: %TEST_DIR%
echo Application location: %APP_DIR%
echo.

echo Testing all sample files...
echo.

cd /d "%APP_DIR%"

echo 1. Listing available extractors:
echo --------------------------------
dotnet run -- --list-extractors
echo.

echo 2. Showing extractor statistics:
echo --------------------------------
dotnet run -- --extractor-stats
echo.

echo 3. Testing individual files:
echo ----------------------------

echo Testing sample.txt:
dotnet run -- --test-extraction "%TEST_DIR%sample.txt"
echo.

echo Testing README.md:
dotnet run -- --test-extraction "%TEST_DIR%README.md"
echo.

echo Testing application.log:
dotnet run -- --test-extraction "%TEST_DIR%application.log"
echo.

echo Testing sample-data.csv:
dotnet run -- --test-extraction "%TEST_DIR%sample-data.csv"
echo.

echo Testing index.html:
dotnet run -- --test-extraction "%TEST_DIR%index.html"
echo.

echo Testing page.htm:
dotnet run -- --test-extraction "%TEST_DIR%page.htm"
echo.

echo Testing contents.hhc:
dotnet run -- --test-extraction "%TEST_DIR%contents.hhc"
echo.

echo 4. Running audit on test directory:
echo ----------------------------------
dotnet run -- --audit "%TEST_DIR%"
echo.

echo Testing complete!
echo.
echo To run interactive extractor management:
echo dotnet run
echo Then select option 16
echo.
pause