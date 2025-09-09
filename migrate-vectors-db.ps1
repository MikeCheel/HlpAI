# Vector Database Migration Script
# This script moves existing vectors.db files to the centralized location

Write-Host "Vector Database Migration Script" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green

# Get the target directory (user home/.hlpai)
$userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$targetDir = Join-Path $userProfile ".hlpai"
$targetPath = Join-Path $targetDir "vectors.db"

# Create target directory if it doesn't exist
if (-not (Test-Path $targetDir)) {
    Write-Host "Creating directory: $targetDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# Find all vectors.db files in the project
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$vectorDbFiles = Get-ChildItem -Path $projectRoot -Name "vectors.db" -Recurse | Where-Object { $_.FullName -notlike "*\.git\*" -and $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" }

Write-Host "Found $($vectorDbFiles.Count) vectors.db files:" -ForegroundColor Cyan
foreach ($file in $vectorDbFiles) {
    $fullPath = Join-Path $projectRoot $file
    $size = (Get-Item $fullPath).Length
    Write-Host "  - $file ($([math]::Round($size/1KB, 2)) KB)" -ForegroundColor White
}

if ($vectorDbFiles.Count -eq 0) {
    Write-Host "No vectors.db files found to migrate." -ForegroundColor Yellow
    exit 0
}

# Check if target already exists
if (Test-Path $targetPath) {
    $targetSize = (Get-Item $targetPath).Length
    Write-Host "Target file already exists: $targetPath ($([math]::Round($targetSize/1KB, 2)) KB)" -ForegroundColor Yellow
    
    $response = Read-Host "Do you want to replace it? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Host "Migration cancelled." -ForegroundColor Red
        exit 1
    }
    
    # Create backup
    $backupPath = "$targetPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Write-Host "Creating backup: $backupPath" -ForegroundColor Yellow
    Copy-Item $targetPath $backupPath
}

# Find the largest vectors.db file to use as the primary
$largestFile = $null
$largestSize = 0

foreach ($file in $vectorDbFiles) {
    $fullPath = Join-Path $projectRoot $file
    $size = (Get-Item $fullPath).Length
    if ($size -gt $largestSize) {
        $largestSize = $size
        $largestFile = $fullPath
    }
}

if ($largestFile) {
    Write-Host "Migrating largest file: $largestFile ($([math]::Round($largestSize/1KB, 2)) KB)" -ForegroundColor Green
    Copy-Item $largestFile $targetPath -Force
    Write-Host "✅ Migration completed successfully!" -ForegroundColor Green
} else {
    Write-Host "❌ No suitable file found for migration." -ForegroundColor Red
    exit 1
}

# Clean up old files (optional)
$cleanup = Read-Host "Do you want to delete the old vectors.db files? (y/N)"
if ($cleanup -eq 'y' -or $cleanup -eq 'Y') {
    Write-Host "Cleaning up old files..." -ForegroundColor Yellow
    foreach ($file in $vectorDbFiles) {
        $fullPath = Join-Path $projectRoot $file
        Remove-Item $fullPath -Force
        Write-Host "  - Deleted: $file" -ForegroundColor Gray
    }
    
    # Also clean up journal files
    $journalFiles = Get-ChildItem -Path $projectRoot -Name "vectors.db-journal" -Recurse | Where-Object { $_.FullName -notlike "*\.git\*" -and $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" }
    foreach ($file in $journalFiles) {
        $fullPath = Join-Path $projectRoot $file
        Remove-Item $fullPath -Force
        Write-Host "  - Deleted: $file" -ForegroundColor Gray
    }
    
    Write-Host "✅ Cleanup completed!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Migration Summary:" -ForegroundColor Green
Write-Host "- Target location: $targetPath" -ForegroundColor White
Write-Host "- File size: $([math]::Round((Get-Item $targetPath).Length/1KB, 2)) KB" -ForegroundColor White
Write-Host "- All vector database operations will now use this centralized location." -ForegroundColor White