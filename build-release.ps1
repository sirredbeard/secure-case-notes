#!/usr/bin/env pwsh
# Build script for Secure Case Notes v0.0.1 release binaries

param(
    [string]$Version = "0.0.1"
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Building Secure Case Notes v$Version" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Clean
Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean -c Release -v quiet
Remove-Item -Path "publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "release" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path "release" -Force | Out-Null

# Build configurations
$builds = @(
    @{Runtime="win-x64"; Name="Windows x64"; Icon="🪟"},
    @{Runtime="win-arm64"; Name="Windows ARM64"; Icon="🪟"},
    @{Runtime="osx-arm64"; Name="macOS ARM64"; Icon="🍎"}
)

foreach ($build in $builds) {
    Write-Host ""
    Write-Host "$($build.Icon) Building $($build.Name)..." -ForegroundColor Green
    
    $outputPath = "publish/$($build.Runtime)"
    
    dotnet publish src/CaseNotes.Api/CaseNotes.Api.csproj `
        -r $($build.Runtime) `
        -c Release `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $outputPath `
        -v minimal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed for $($build.Name)" -ForegroundColor Red
        exit 1
    }
    
    # Create zip
    Write-Host "  📦 Creating zip archive..." -ForegroundColor Cyan
    $zipName = "CaseNotes-v$Version-$($build.Runtime).zip"
    
    # Copy just the executable and required files
    $tempDir = "release/temp-$($build.Runtime)"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    if ($build.Runtime -like "win-*") {
        Copy-Item "$outputPath/casenotes.exe" $tempDir -Force
    } else {
        Copy-Item "$outputPath/casenotes" $tempDir -Force
    }
    
    # Create README in zip
    @"
# Secure Case Notes v$Version

## Quick Start

1. Run the executable:
   - Windows: Double-click casenotes.exe
   - macOS: chmod +x casenotes && ./casenotes

2. Browser opens to http://localhost:5000

3. First run downloads AI models (~2.6GB, 5-10 minutes)

4. Record notes and generate structured SOAP notes

## Debug Mode

For verbose logging (PHI in console):
- Windows: casenotes.exe --debug
- macOS: ./casenotes --debug

## Documentation

https://github.com/YOUR_USERNAME/secure-case-notes

## HIPAA Notice

All processing is local and in-memory only. No data is written to disk.
Copy generated notes to your EHR immediately.
"@ | Out-File -FilePath "$tempDir/README.txt" -Encoding UTF8
    
    # Create zip
    Compress-Archive -Path "$tempDir/*" -DestinationPath "release/$zipName" -Force
    Remove-Item $tempDir -Recurse -Force
    
    Write-Host "  ✅ $zipName created" -ForegroundColor Green
    
    # Show size
    $size = (Get-Item "release/$zipName").Length / 1MB
    Write-Host "  📊 Size: $([math]::Round($size, 2)) MB" -ForegroundColor Gray
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  ✅ All builds complete!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Release artifacts in:" -ForegroundColor Yellow
Get-ChildItem release/*.zip | ForEach-Object {
    $size = $_.Length / 1MB
    Write-Host "  📦 $($_.Name) ($([math]::Round($size, 2)) MB)" -ForegroundColor White
}
Write-Host ""
