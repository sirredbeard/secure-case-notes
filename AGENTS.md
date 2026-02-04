# AI Development Agents Guide

This document describes the AI agents and tools available for developing and extending the Secure Case Notes application.

## Project Requirements

### Cross-Platform & Cross-Architecture Support

**CRITICAL**: This application MUST work on:
- ✅ **Windows** (x64, ARM64)
- ✅ **macOS** (x64, ARM64/Apple Silicon)  
- ✅ **Linux** (x64, ARM64) - possible future support

**Testing principle**: If it works on one platform/architecture, it MUST work on all others.

**Why this matters**:
- No platform-specific native libraries
- No architecture-specific binaries
- Pure ONNX models via Microsoft.ML.OnnxRuntime
- Same code, same packages, everywhere

### What We've Tried & Ruled Out

#### ❌ Whisper.NET
- **Problem**: Requires platform-specific runtimes
  - Windows: `Whisper.net.Runtime` (x64 only) or `Whisper.net.Runtime.NoAvx` (broken package)
  - macOS: `Whisper.net.Runtime.CoreML` (different package)
  - Windows ARM64: No native support (would require x64 emulation)
- **Ruled out**: Cannot guarantee "test once, works everywhere"

#### ❌ Vosk
- **Problem**: Native C++ libraries with limited architecture support
  - Windows ARM64: Not supported natively (x64 only)
  - Requires different binaries per platform
- **Ruled out**: Platform-specific issues

#### ❌ Silero Models  
- **Problem**: Models not publicly accessible
  - HuggingFace repo requires authentication
  - Official download URLs return 404/401
- **Ruled out**: Cannot reliably download

#### ✅ Sherpa-ONNX - IMPLEMENTED
- **Package**: `org.k2fsa.sherpa.onnx` v1.12.23 ✅ Installed
- **Status**: ✅ **FULLY IMPLEMENTED** - Build successful, ready for testing
- **Model**: Zipformer transducer (encoder + decoder + joiner)
- **Download**: ~76MB from GitHub releases
- **Platform runtimes included**:
  - ✅ Windows x64 + ARM64 + x86
  - ✅ macOS x64 + ARM64 (Apple Silicon)
  - ✅ Linux x64 + ARM64
- **Implementation**:
  - ✅ ModelManager updated with Sherpa model download
  - ✅ TranscriptionService rewritten with Sherpa-ONNX API
  - ✅ Audio pipeline: WebM → 16kHz mono → float samples → Sherpa
  - ✅ Returns REAL transcription (not mock!)
- **Pro**: Test once on Windows ARM64 = guaranteed to work on macOS ARM64! 🎯
- **GitHub**: https://github.com/k2-fsa/sherpa-onnx

### Current Implementation Status

**Code Quality** ✅:
- ✅ **Deep code review completed** (February 4, 2026)
- ✅ **All critical issues fixed**:
  - Fixed Sherpa model filename mismatch (encoder-epoch-99-avg-1...)
  - Fixed DownloadProgressTracker model name checks (Whisper → Sherpa pattern matching)
  - Fixed Config disposal issue in TextGenerationService
  - Fixed temp file race condition (now uses Guid.NewGuid())
  - Fixed MediaFoundationResampler resource leak (now using statement)
  - Corrected Phi-3 vs Phi-4 naming confusion
  - Removed unused code (Class1.cs, UnitTest1.cs, ExtractGeneratedText, Reset methods)
  - Removed duplicate package reference (Microsoft.ML.OnnxRuntimeGenAI.Managed from API)
  - Updated tests to expect correct model names
  - Enhanced HIPAA logging filters (excludes PHI keywords, file logging only in debug)
## Current Implementation Status (v0.0.1) 🎉

**Milestone**: ✅ **FULLY WORKING END-TO-END**

### What's Working ✅

1. **Speech-to-Text**: Sherpa-ONNX Zipformer
   - ✅ Accurate transcription (~9x realtime, 0.09 RTF)
   - ✅ 43-chunk decoding for 14-second audio
   - ✅ Handles WebM browser recording perfectly
   - ✅ Auto-downsampling to 16kHz mono

2. **Text Generation**: Phi-3-mini-4k-instruct
   - ✅ Real AI-powered SOAP notes (162 tokens in 42 seconds)
   - ✅ Anti-hallucination system prompt prevents fabrication
   - ✅ Proper ONNX Runtime GenAI integration
   - ✅ Task.Run() prevents UI blocking

3. **Application Stability**:
   - ✅ Multi-note sessions (no crash after first note)
   - ✅ SignalR timeout fixes (5-minute generation window)
   - ✅ InvokeAsync(StateHasChanged) for safe UI updates
   - ✅ Circuit retention for long sessions

4. **HIPAA Compliance**:
   - ✅ No disk writes for PHI (in-memory only)
   - ✅ Console-only logging with `--debug` flag
   - ✅ Temp files immediately deleted
   - ✅ No telemetry

5. **Cross-Platform**:
   - ✅ Windows x64 + ARM64
   - ✅ macOS ARM64 (Apple Silicon)
   - ✅ Single NuGet package works everywhere

### Performance Metrics

| Metric | Value |
|--------|-------|
| **Transcription** | 2-3 seconds (14s audio → 43 chunks) |
| **Generation** | 30-60 seconds (3-4 tokens/sec) |
| **Model Load** | ~3 seconds (first use per session) |
| **Memory** | ~4GB with models loaded |

### Known Issues & Limitations

- ⚠️ **Generation is slow** (CPU-only INT4, 3-4 tokens/sec) - expected on ARM64
- ⚠️ **No streaming generation** - full note appears at once
- ⚠️ **Brief audio may have lower accuracy** - Sherpa optimized for longer clips

## Overview

**Best practices**:
- Write clear, descriptive comments
- Use consistent naming conventions
- Review all generated code before committing
- Ensure HIPAA compliance in any generated code

## ONNX Runtime GenAI Documentation

**Purpose**: Understanding model integration  
**Resources**:
- [ONNX Runtime GenAI Docs](https://onnxruntime.ai/docs/genai/)
- [C# API Reference](https://onnxruntime.ai/docs/genai/api/csharp.html)
- [Model Zoo](https://github.com/onnx/models)

**Key concepts**:
- Model loading and initialization
- Tokenizer usage
- Generator parameters
- Streaming generation
- Memory management

## Development Workflows

### Adding a New Feature

1. **Planning**:
   ```
   Ask Copilot Chat: "I want to add support for Spanish transcription. 
   What changes are needed to the TranscriptionService?"
   ```

2. **Implementation**:
   - Use Copilot for code generation
   - Review for HIPAA compliance
   - Add logging statements

3. **Testing**:
   - Generate unit tests with Copilot
   - Manual testing with real audio/transcripts
   - CI/CD validation

4. **Documentation**:
   - Update README.md
   - Add inline comments for complex logic
   - Update ONNX_INTEGRATION.md if needed

### Debugging Issues

1. **Identify the problem**:
   ```
   Ask Copilot Chat: "The Whisper model is failing to load. 
   Here's the error: [paste error]. What could be wrong?"
   ```

2. **Review logs**:
   - Check `logs/casenotes-*.txt`
   - Look for ERROR or WARNING messages
   - Check model download status

3. **Test in isolation**:
   - Create a minimal reproduction case
   - Test ONNX Runtime components separately
   - Verify model files are complete

4. **Fix and validate**:
   - Apply the fix
   - Run unit tests
   - Test end-to-end workflow

### Performance Optimization

1. **Profile the application**:
   ```csharp
   // Use Copilot to add performance logging
   var stopwatch = Stopwatch.StartNew();
   // ... code to profile ...
   _logger.LogInformation("Operation took {Ms}ms", stopwatch.ElapsedMilliseconds);
   ```

2. **Identify bottlenecks**:
   - Model loading time
   - Inference speed
   - Memory usage
   - Audio processing

3. **Optimize**:
   - Cache loaded models ✓ (already implemented)
   - Use quantized models ✓ (Int4)
   - Batch processing when possible
   - Async operations

## Code Quality Standards

### HIPAA Compliance

**Always verify**:
- No patient data written to disk
- All processing in memory
- No external API calls with PHI
- Proper disposal of sensitive data

### Testing Requirements

**Required tests**:
- Unit tests for all public methods
- Integration tests for API endpoints
- HIPAA compliance tests
- Memory leak tests

**Generate tests with Copilot**:
```csharp
// Type: /tests
// Copilot will suggest relevant test cases
```

### Documentation Standards

**What to document**:
- Complex algorithms
- ONNX-specific implementations
- HIPAA compliance measures
- Performance considerations

**Don't over-document**:
- Simple getters/setters
- Obvious logic
- Framework-standard patterns

## Model Integration Workflow

### Adding a New Model

1. **Research the model**:
   - Find ONNX-compatible version on HuggingFace
   - Check input/output format
   - Verify licensing for healthcare use

2. **Update ModelManager**:
   ```csharp
   // Add new ModelInfo
   private static readonly ModelInfo NewModelInfo = new()
   {
       Name = "NewModel",
       HuggingFaceRepo = "org/model-name-onnx",
       Variant = "cpu-int4",
       EstimatedSizeBytes = 1_000_000_000
   };
   ```

3. **Create service interface**:
   ```csharp
   public interface INewModelService : IDisposable
   {
       Task<Result> ProcessAsync(Request request, CancellationToken ct);
       Task<bool> IsReadyAsync(CancellationToken ct);
   }
   ```

4. **Implement service**:
   - Use existing services as templates
   - Initialize model in constructor
   - Process data in-memory only
   - Handle errors gracefully

5. **Add API endpoint**:
   ```csharp
   app.MapPost("/api/newmodel", async (Request req, INewModelService svc, CT ct) =>
   {
       var result = await svc.ProcessAsync(req, ct);
       return result.Success ? Results.Ok(result) : Results.BadRequest(result);
   });
   ```

6. **Update UI**:
   - Add new button/control in Blazor
   - Call new API endpoint
   - Display results

7. **Test thoroughly**:
   - Unit tests
   - Integration tests
   - Load tests
   - HIPAA compliance verification

### Model Testing Checklist

- [ ] Model downloads successfully
- [ ] Model loads without errors
- [ ] Input preprocessing works correctly
- [ ] Inference produces expected output
- [ ] Output postprocessing is correct
- [ ] Memory is properly disposed
- [ ] No disk writes for patient data
- [ ] Performance meets requirements
- [ ] Error handling is comprehensive
- [ ] Logging doesn't expose PHI

## Continuous Improvement

### Monitoring Model Performance

**Add telemetry (non-PHI only)**:
```csharp
_logger.LogInformation(
    "Transcription completed: duration={Duration}ms, audioSize={Size}bytes",
    stopwatch.ElapsedMilliseconds,
    request.AudioData.Length);
```

**Track metrics**:
- Model loading time
- Inference speed
- Memory usage
- Error rates
- User satisfaction (if available)

### Updating Models

**When to update**:
- Better accuracy available
- Smaller model size
- Faster inference
- Bug fixes in model

**Update process**:
1. Test new model version separately
2. Compare accuracy with current version
3. Verify HIPAA compliance maintained
4. Update model configuration
5. Deploy with rollback plan
6. Monitor for issues

### Generated Code Has Issues

1. Review carefully before accepting
2. Test thoroughly
3. Check for HIPAA violations
4. Validate with existing patterns
5. Refactor if needed

### Model Integration Problems

1. Check model compatibility
2. Verify ONNX Runtime version
3. Review input/output formats
4. Test model independently
5. Check HuggingFace model card

## Additional Resources

### Documentation
- [.NET 10 Documentation](https://learn.microsoft.com/en-us/dotnet/)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [ONNX Runtime](https://onnxruntime.ai/)
- [HuggingFace Hub](https://huggingface.co/docs/hub)

### Community
- [ONNX Runtime GitHub](https://github.com/microsoft/onnxruntime)
- [ONNX Runtime GenAI](https://github.com/microsoft/onnxruntime-genai)
- [Stack Overflow - ONNX](https://stackoverflow.com/questions/tagged/onnx)

### Healthcare AI
- [HIPAA Compliance Guide](https://www.hhs.gov/hipaa/)
- [FDA Software as Medical Device](https://www.fda.gov/medical-devices/digital-health-center-excellence/software-medical-device-samd)
- [Healthcare AI Ethics](https://www.who.int/publications/i/item/9789240029200)


**HIPAA Warning**: AI tools like Copilot should NEVER see actual patient data. Only use with synthetic/test data.

Do not list credits, support, or contribution guidelines in documentation.