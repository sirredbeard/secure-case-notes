# Secure Case Notes v0.0.1

**HIPAA-Compliant AI-Assisted Mental Health Case Note Documentation**

A cross-platform desktop application that uses local AI models to transcribe therapy sessions and generate structured clinical case notes, with complete HIPAA compliance through in-memory-only processing.

## ✨ What's New in v0.0.1

- ✅ **Full speech-to-text working** with Sherpa-ONNX Zipformer
- ✅ **Real Phi-3 generation** producing clinical SOAP notes
- ✅ **Anti-hallucination system prompt** prevents fabricated information
- ✅ **Multi-note sessions** - app stays alive for continuous use
- ✅ **Comprehensive debugging** with `--debug` flag (console-only PHI logging)
- ✅ **Cross-platform builds** for Windows x64/ARM64 and macOS ARM64

## Features

- 🎤 **Audio Recording**: Browser-based recording with microphone permission
- 🗣️ **Speech-to-Text**: Sherpa-ONNX Zipformer for accurate real-time transcription (~9x realtime speed)
- 🤖 **AI-Powered Generation**: Phi-3-mini-4k-instruct for structured SOAP notes (3-4 tokens/sec on CPU)
- 📥 **Automatic Model Download**: Models download automatically on first run with progress indicator
- 🔒 **HIPAA Compliant**: No data persistence - all processing in memory only, no disk writes
- 🖥️ **Cross-Platform**: Runs on Windows x64/ARM64 and macOS ARM64
- 📦 **Single Executable**: Self-contained deployment with auto-browser launch
- 🌐 **Local-Only**: No internet required after initial model download
- ⚡ **ONNX Runtime**: High-performance local AI inference (CPU optimized, INT4 quantization)

## Quick Start

### Download Pre-Built Binary (Recommended)

1. Download for your platform from [Releases](https://github.com/YOUR_USERNAME/secure-case-notes/releases):
   - `CaseNotes-v0.0.1-win-x64.zip` (Windows 64-bit)
   - `CaseNotes-v0.0.1-win-arm64.zip` (Windows ARM64)
   - `CaseNotes-v0.0.1-osx-arm64.zip` (macOS Apple Silicon)

2. Extract and run:
   - **Windows**: Double-click `casenotes.exe`
   - **macOS**: `chmod +x casenotes && ./casenotes`

3. Browser opens automatically to `http://localhost:5000`

4. First run downloads AI models (~2.6GB) - wait for completion

### Build from Source

```bash
# Clone repository
git clone https://github.com/YOUR_USERNAME/secure-case-notes.git
cd secure-case-notes

# Run with debug logging
cd src/CaseNotes.Api
dotnet run -- --debug

# Or production mode (silent)
dotnet run
```

## System Requirements

- **Disk Space**: ~2.6GB for AI models (auto-downloaded on first run)
- **RAM**: 4GB minimum, 8GB recommended
- **Processor**: Any modern CPU (ARM64 or x64)
- **Internet**: Required only for initial model download

**AI Models** (downloaded automatically):
- Sherpa-ONNX Zipformer: ~76MB (speech-to-text, INT8)
- Phi-3-mini-4k-instruct: ~2.5GB (text generation, INT4)
- Microphone access for audio recording

## Supported Platforms

✅ **Windows** (x64, ARM64)  
✅ **macOS** (x64, ARM64/Apple Silicon)  
✅ **Linux** (x64, ARM64) - Experimental

**Cross-platform design**: Same code and packages work on all platforms. Test once on Windows = works on macOS!

## Quick Start

### 1. Prerequisites

- .NET 10 SDK
- ~3GB disk space for models (auto-downloaded)

### 2. Run the Application

```bash
# Clone the repository
git clone https://github.com/your-org/secure-case-notes.git
cd secure-case-notes

# Build and run
cd src/CaseNotes.Api
dotnet run
```

On first run, the application will:
1. Automatically download AI models (Sherpa + Phi-3, ~2.6GB total)
2. Cache models in OS-specific location:
   - Windows: `%LOCALAPPDATA%\SecureCaseNotes\models`
   - macOS: `~/Library/Application Support/SecureCaseNotes/models`
   - Linux: `~/.local/share/SecureCaseNotes/models`
3. Initialize Sherpa-ONNX and Phi-3 models
4. Open browser to http://localhost:5000

### 3. Use the Application

## Usage

### Running the Application

**Development Mode (with verbose logging)**:
```bash
cd src/CaseNotes.Api
dotnet run -- --debug
```

**Production Mode (silent)**:
```bash
cd src/CaseNotes.Api
dotnet run
```

The app will:
1. Start server on `http://localhost:5000`
2. Auto-open your default browser (production) or wait (debug)
3. Download AI models on first run (~2.6GB, 5-10 minutes)
4. Show "All models ready" when complete

### Recording Your First Note

1. **Click "Start Recording"** - browser requests microphone permission
2. **Speak your case notes** - dictate SOAP format or free-form
3. **Click "Stop Recording"** - transcription happens automatically (~2-3 seconds)
4. **Wait for generation** - Phi-3 structures the note (30-60 seconds, watch CPU)
5. **Review structured note** - SOAP format with sections
6. **Copy to clipboard** - paste into your EHR system immediately
7. **Record next note** - app stays alive for multiple sessions

### Debug vs Production Mode

| Feature | `--debug` | Production (default) |
|---------|-----------|---------------------|
| Console logging | ✅ Verbose (PHI visible) | ❌ None |
| Log to disk | ❌ Never | ❌ Never |
| Console window | ✅ Visible | ❌ Hidden (Windows) |
| Browser auto-open | ❌ Manual | ✅ Automatic |
| Performance | Same | Same |

**HIPAA Note**: Even in debug mode, PHI is NEVER written to disk - only displayed in console for troubleshooting.

### 3. Build Single-File Executable (Optional)

For production deployment:

**macOS ARM64**:
```bash
./publish-osx.sh
# or manually:
dotnet publish src/CaseNotes.Api/CaseNotes.Api.csproj \
  -r osx-arm64 -c Release --self-contained \
  -p:PublishSingleFile=true -o publish/osx-arm64
```

**Windows ARM64**:
```powershell
.\publish-win.ps1
# or manually:
dotnet publish src/CaseNotes.Api/CaseNotes.Api.csproj `
  -r win-arm64 -c Release --self-contained `
  -p:PublishSingleFile=true -o publish/win-arm64
```

The executable will auto-download models on first run.

## HIPAA Compliance

This application implements the following HIPAA safeguards:

### Technical Safeguards
- ✅ **No Persistent Storage**: Audio and notes NEVER written to disk
- ✅ **In-Memory Processing**: All PHI processed in RAM only
- ✅ **Local Processing**: No data transmitted to external servers
- ✅ **Temp File Cleanup**: Audio conversion uses unique temp files, immediately deleted
- ✅ **Telemetry Disabled**: No usage data collected
- ✅ **Filtered Logging**: PHI keywords excluded from logs (debug mode only)
- ✅ **Secure Cleanup**: All resources properly disposed after each request

### Administrative Safeguards
- ✅ **Access Control**: Runs on user's local machine only
- ✅ **Session Isolation**: Each session independent
- ✅ **Clear Instructions**: Prominent HIPAA notices in UI

### Physical Safeguards
- ✅ **Local-Only Deployment**: No network dependencies after setup
- ✅ **User-Controlled**: Professional manages all data

### Important HIPAA Notices

⚠️ **Data Responsibility**: The structured case note appears only in your browser memory. You MUST copy it to your secure EHR system immediately. Do not refresh or close the browser without copying the note.

## Project Structure

```
secure-case-notes/
├── src/
│   ├── CaseNotes.Api/           # ASP.NET Core Web API
│   │   ├── Program.cs           # API configuration & endpoints
│   │   └── wwwroot/
│   │       └── examples.json    # Few-shot case note examples
│   ├── CaseNotes.Ui/            # Blazor Server UI
│   │   ├── Components/
│   │   │   └── Pages/
│   │   │       └── Home.razor   # Main recording interface
│   │   └── wwwroot/
│   │       └── js/
│   │           └── audio-recorder.js  # MediaRecorder integration
│   └── CaseNotes.Core/          # Shared library
│       ├── Models/              # Data models
│       ├── Interfaces/          # Service contracts
│       └── Services/            # ONNX Runtime services
│           ├── ModelManager.cs
│           ├── TranscriptionService.cs
│           └── TextGenerationService.cs
├── tests/
│   └── CaseNotes.Tests/         # Unit & integration tests
├── .github/
│   └── workflows/
│       └── build.yml            # CI/CD pipeline
└── CaseNotes.sln
```

## Performance Metrics

**Tested on**: Windows ARM64 (Snapdragon X Elite)

| Component | Speed | Model Size |
|-----------|-------|------------|
| **Speech-to-Text** (Sherpa-ONNX) | ~9x realtime (0.09 RTF) | 76MB |
| **Text Generation** (Phi-3) | 3-4 tokens/sec | 2.5GB |
| **End-to-end** | ~2-3s transcribe + 30-60s generation | - |

**Example**: 14-second audio → 2 seconds transcription → 42 seconds generation (162 tokens)

## Known Limitations

- **Generation Time**: Phi-3 CPU inference takes 30-60 seconds per note
- **No Streaming**: Generation shows as single output (not word-by-word)
- **Brief Transcripts**: Very short recordings may have less accurate transcription
- **English Only**: Models trained primarily on English (other languages not tested)
- **No Edit History**: Each note is independent - no versioning or revision tracking

## Troubleshooting

### Model Cache Locations

- **Windows**: `%LOCALAPPDATA%\SecureCaseNotes\models\`
- **macOS**: `~/Library/Application Support/SecureCaseNotes/models/`
- **Linux**: `~/.local/share/SecureCaseNotes/models/`

### API Configuration

Edit `appsettings.json` in `CaseNotes.Api`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      }
    }
  }
}
```

## Development

### Running Tests

```bash
dotnet test
```

### Building

```bash
dotnet build
```

### Debugging

1. Set breakpoints in VS Code
2. Start the API project in debug mode
3. Start the UI project
4. Navigate to `http://localhost:5000`

## Troubleshooting

### Models Not Loading

**Problem**: "Models not found" error on startup

**Solution**: Verify models were downloaded to correct location:
```bash
# Windows
dir "%LOCALAPPDATA%\SecureCaseNotes\models"

# macOS/Linux
ls ~/Library/Application\ Support/SecureCaseNotes/models/
```

### Microphone Permission Denied

**Problem**: Cannot start recording

**Solution**: 
- Check browser permissions for microphone access
- Ensure no other application is using the microphone
- Try a different browser (Edge recommended)

### Transcription Not Working

**Problem**: "Transcription failed" error

**Solution**:
- Verify Sherpa-ONNX model is properly downloaded (check for encoder/decoder/joiner files)
- Hard refresh browser (Ctrl+Shift+R / Cmd+Shift+R) to clear Blazor cache
- Check audio format compatibility (WebM supported)
- Review API logs (debug mode only, in `logs/casenotes-*.txt`)

### Generation Timeout

**Problem**: Case note generation takes too long or fails

**Solution**:
- Phi-3 CPU inference can take 30-60 seconds for long transcripts
- Ensure sufficient RAM (8GB+ recommended)
- Reduce transcript length if too long (split into multiple sessions)

## Performance

### System Requirements

**Minimum**:
- CPU: Modern x64 or ARM64 processor
- RAM: 8GB (16GB recommended)
- Storage: 3GB free for models

**Recommended**:
- CPU: Apple Silicon (M1/M2/M3), Intel Core i5+, AMD Ryzen 5+, or Qualcomm Snapdragon X Elite
- RAM: 16GB+ for faster inference
- Storage: 5GB free (for models + working space)

### Inference Times (Typical on ARM64)

- **Sherpa-ONNX Transcription**: <0.1x realtime (1 min audio → 6 sec processing) - 9x faster than realtime!
- **Phi-3 Generation**: 20-60 seconds (depending on transcript length, ~10-15 tokens/sec on CPU)

## Technology Stack

- **.NET 10**: Latest C# features and performance improvements
- **ASP.NET Core**: Web API with minimal overhead
- **Blazor Server**: Interactive UI with SignalR
- **ONNX Runtime GenAI**: High-performance model inference (Phi-3)
- **Sherpa-ONNX**: Cross-platform speech recognition (k2-fsa project)
- **NAudio**: Audio processing and format conversion
- **Serilog**: Structured logging with PHI filtering

## Contributing

This project follows clean architecture principles and HIPAA compliance requirements. When contributing:

1. Never log PHI (patient health information)
2. Ensure all audio/text processing stays in memory
3. Test on multiple platforms (Windows, macOS, Linux)
4. Follow .NET 10 coding conventions
5. Include unit tests for new features

## Disclaimer

⚠️ **Medical Use Disclaimer**: This software is provided as-is for assisting mental health professionals with documentation. It does not replace professional judgment and should be used as a tool only. Always review and edit generated notes before adding to official patient records.

⚠️ **HIPAA Responsibility**: While this application implements technical safeguards for HIPAA compliance, the ultimate responsibility for compliance rests with the healthcare provider. Ensure your organization's policies permit use of AI tools for clinical documentation.

## License

[Include your license here]

## Acknowledgments

- **Sherpa-ONNX**: Next-generation Kaldi (k2-fsa) - https://github.com/k2-fsa/sherpa-onnx
- **Microsoft ONNX Runtime GenAI**: https://github.com/microsoft/onnxruntime-genai
- **Phi-3 Model**: Microsoft Research
