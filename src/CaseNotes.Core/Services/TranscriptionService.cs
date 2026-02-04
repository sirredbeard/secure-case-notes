using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SherpaOnnx;
using CaseNotes.Core.Interfaces;
using CaseNotes.Core.Models;

namespace CaseNotes.Core.Services;

/// <summary>
/// Sherpa-ONNX based speech-to-text service
/// Cross-platform: Windows, macOS, Linux (x64, ARM64)
/// Uses Zipformer transducer model for real English speech recognition
/// </summary>
public class TranscriptionService(IModelManager modelManager, ILogger<TranscriptionService> logger) : ITranscriptionService
{
    private readonly IModelManager _modelManager = modelManager;
    private readonly ILogger<TranscriptionService> _logger = logger;
    private OnlineRecognizer? _recognizer;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            return _isInitialized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if transcription service is ready");
            return false;
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;
            
            _logger.LogInformation("Initializing Sherpa-ONNX Zipformer...");
            
            var modelPath = await _modelManager.GetSherpaModelPathAsync(cancellationToken);
            
            if (!Directory.Exists(modelPath))
            {
                _logger.LogWarning("Sherpa model not found at {Path}", modelPath);
                return;
            }
            
            // Create Sherpa-ONNX config for streaming Zipformer transducer
            var config = new OnlineRecognizerConfig
            {
                FeatConfig = new FeatureConfig
                {
                    SampleRate = 16000,
                    FeatureDim = 80
                },
                ModelConfig = new OnlineModelConfig
                {
                    Transducer = new OnlineTransducerModelConfig
                    {
                        Encoder = Path.Combine(modelPath, "encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx"),
                        Decoder = Path.Combine(modelPath, "decoder-epoch-99-avg-1-chunk-16-left-128.onnx"),
                        Joiner = Path.Combine(modelPath, "joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx")
                    },
                    Tokens = Path.Combine(modelPath, "tokens.txt"),
                    NumThreads = Environment.ProcessorCount > 2 ? 2 : 1, // Use 2 threads if available
                    Debug = 0
                },
                DecodingMethod = "greedy_search", // Fastest decoding
                MaxActivePaths = 4,
                EnableEndpoint = 0 // Disable endpoint detection for file-based transcription
            };
            
            _logger.LogInformation("Creating Sherpa recognizer...");
            _recognizer = new OnlineRecognizer(config);
            
            _isInitialized = true;
            _logger.LogInformation("✅ Sherpa-ONNX initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Sherpa-ONNX");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🎙️ TRANSCRIBE START: AudioData length={Length}", request.AudioData?.Length ?? 0);
            
            await EnsureInitializedAsync(cancellationToken);
            
            _logger.LogInformation("✅ EnsureInitializedAsync completed");
            
            if (_recognizer == null)
            {
                _logger.LogError("❌ Recognizer is NULL after initialization!");
                return new TranscriptionResult 
                { 
                    Text = string.Empty,
                    Success = false,
                    ErrorMessage = "Sherpa-ONNX model not initialized"
                };
            }
            
            _logger.LogInformation("Converting audio to 16kHz mono samples...");
            var samples = await ConvertAudioToSamplesAsync(request.AudioData, cancellationToken);
            _logger.LogInformation("✅ Audio converted: {SampleCount} samples", samples.Length);
            
            _logger.LogInformation("Creating Sherpa stream and feeding audio ({Length} samples)...", samples.Length);
            
            // Run transcription on background thread to avoid blocking UI
            var transcribedText = await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("🔄 Task.Run: Creating Sherpa stream...");
                    // Create a stream for this transcription
                    using var stream = _recognizer.CreateStream();
                    _logger.LogInformation("✅ Sherpa stream created");
                    
                    // Feed the audio samples to Sherpa
                    _logger.LogInformation("Feeding {Count} samples to Sherpa...", samples.Length);
                    stream.AcceptWaveform(16000, samples);
                    _logger.LogInformation("✅ Audio fed to Sherpa");
                    
                    // Signal that we're done feeding audio
                    _logger.LogInformation("Calling InputFinished()...");
                    stream.InputFinished();
                    _logger.LogInformation("✅ InputFinished() complete");
                    
                    // Decode the audio
                    _logger.LogInformation("Starting decode loop...");
                    int decodeCount = 0;
                    while (_recognizer.IsReady(stream))
                    {
                        _logger.LogInformation("  Decoding chunk {Count}...", decodeCount + 1);
                        _recognizer.Decode(stream);
                        decodeCount++;
                        _logger.LogInformation("  ✅ Chunk {Count} decoded", decodeCount);
                    }
                    _logger.LogInformation("✅ Decode loop complete: {Total} chunks", decodeCount);
                    
                    // Get the result
                    _logger.LogInformation("Calling GetResult()...");
                    var result = _recognizer.GetResult(stream);
                    _logger.LogInformation("✅ GetResult() complete");
                    
                    _logger.LogInformation("🎉 TRANSCRIPTION SUCCESS: '{Text}'", result.Text);
                    return result.Text.Trim();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 SHERPA DECODE CRASHED: {Message}", ex.Message);
                    _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                    return string.Empty;
                }
            }, cancellationToken);
            
            _logger.LogInformation("✅ Task.Run completed, text length={Length}", transcribedText?.Length ?? 0);
            
            if (string.IsNullOrEmpty(transcribedText))
            {
                return new TranscriptionResult
                {
                    Text = string.Empty,
                    Success = false,
                    ErrorMessage = "Sherpa transcription returned empty result"
                };
            }
            
            return new TranscriptionResult
            {
                Text = transcribedText,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return new TranscriptionResult 
            { 
                Text = string.Empty,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<float[]> ConvertAudioToSamplesAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        // Save to temp file with unique name (MediaFoundationReader requires file path)
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.webm");
        try
        {
            _logger.LogInformation("💾 Writing temp audio file: {File}", tempFile);
            await File.WriteAllBytesAsync(tempFile, audioData, cancellationToken);
            _logger.LogInformation("✅ Temp file written: {Size} bytes", audioData.Length);
            
            _logger.LogInformation("🎵 Creating MediaFoundationReader...");
            // Try reading as WebM/MP4 first (most common from browser)
            using var reader = new MediaFoundationReader(tempFile);
            _logger.LogInformation("✅ MediaFoundationReader created: {Format}, {Rate}Hz, {Channels}ch", 
                reader.WaveFormat.Encoding, reader.WaveFormat.SampleRate, reader.WaveFormat.Channels);
            
            var result = await ResampleToMonoAsync(reader, cancellationToken);
            _logger.LogInformation("✅ Resampling complete: {Samples} samples", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MediaFoundation failed, trying WAV fallback...");
            // Fallback: Try as WAV
            try
            {
                await using var audioStream = new MemoryStream(audioData);
                using var reader = new WaveFileReader(audioStream);
                _logger.LogInformation("✅ WAV reader created");
                var result = await ResampleToMonoAsync(reader, cancellationToken);
                _logger.LogInformation("✅ WAV resampling complete: {Samples} samples", result.Length);
                return result;
            }
            catch (Exception wavEx)
            {
                _logger.LogError(wavEx, "💥 WAV fallback also failed");
                throw new InvalidOperationException("Failed to read audio data as WebM or WAV", wavEx);
            }
        }
        finally
        {
            // Clean up temp file (HIPAA: no audio persists)
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                    _logger.LogInformation("🗑️ Temp file deleted");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file: {File}", tempFile);
            }
        }
    }

    private static async Task<float[]> ResampleToMonoAsync(WaveStream reader, CancellationToken cancellationToken)
    {
        // Resample to 16kHz mono
        using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 1))
        {
            ResamplerQuality = 60 // High quality
        };
        
        // Convert to float samples
        var sampleProvider = resampler.ToSampleProvider();
        var samples = new List<float>();
        var buffer = new float[16000]; // 1 second buffer
        
        int samplesRead;
        while ((samplesRead = await Task.Run(() => sampleProvider.Read(buffer, 0, buffer.Length), cancellationToken)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                samples.Add(buffer[i]);
            }
        }
        
        return [.. samples];
    }

    public void Dispose()
    {
        _recognizer?.Dispose();
        _initLock?.Dispose();
    }
}
