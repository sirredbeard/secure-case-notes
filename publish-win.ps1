# Build script for Windows ARM64 deployment

Write-Host "Building Secure Case Notes for Windows ARM64..." -ForegroundColor Cyan

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Path "publish\win-arm64" -Recurse -Force -ErrorAction SilentlyContinue

# Build the API (which includes the UI)
Write-Host "Publishing single-file executable..." -ForegroundColor Yellow
dotnet publish src\CaseNotes.Api\CaseNotes.Api.csproj `
  -r win-arm64 `
  -c Release `
  --self-contained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish\win-arm64

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Build successful!" -ForegroundColor Green
    Write-Host "Executable location: publish\win-arm64\casenotes.exe" -ForegroundColor Green
    Write-Host ""
    Write-Host "To run: .\publish\win-arm64\casenotes.exe" -ForegroundColor White
    Write-Host ""
    Write-Host "IMPORTANT: Ensure AI models are downloaded first:" -ForegroundColor Yellow
    Write-Host "  huggingface-cli download openai/whisper-tiny --include `"onnx/*`"" -ForegroundColor White
    Write-Host "  huggingface-cli download microsoft/Phi-4-mini-instruct-onnx --include `"cpu-int4-rtn-block-32-acc-level-4/*`"" -ForegroundColor White
} else {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}
