#!/bin/bash

# Build script for macOS ARM64 deployment

echo "Building Secure Case Notes for macOS ARM64..."

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf publish/osx-arm64

# Build the API (which includes the UI)
echo "Publishing single-file executable..."
dotnet publish src/CaseNotes.Api/CaseNotes.Api.csproj \
  -r osx-arm64 \
  -c Release \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o publish/osx-arm64

if [ $? -eq 0 ]; then
    echo "✓ Build successful!"
    echo "Executable location: publish/osx-arm64/casenotes"
    echo ""
    echo "To run: ./publish/osx-arm64/casenotes"
    echo ""
    echo "IMPORTANT: Ensure AI models are downloaded first:"
    echo "  huggingface-cli download openai/whisper-tiny --include 'onnx/*'"
    echo "  huggingface-cli download microsoft/Phi-4-mini-instruct-onnx --include 'cpu-int4-rtn-block-32-acc-level-4/*'"
else
    echo "✗ Build failed!"
    exit 1
fi
