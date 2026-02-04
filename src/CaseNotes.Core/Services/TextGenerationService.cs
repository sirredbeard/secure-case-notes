using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CaseNotes.Core.Interfaces;
using CaseNotes.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace CaseNotes.Core.Services;

/// <summary>
/// Text generation service using Phi-4-mini-instruct via ONNX Runtime GenAI
/// Implements HIPAA-compliant in-memory processing only
/// </summary>
public sealed class TextGenerationService : ITextGenerationService
{
    private readonly ILogger<TextGenerationService> _logger;
    private readonly IModelManager _modelManager;
    private Model? _model;
    private Tokenizer? _tokenizer;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;
    private bool _disposed;
    private readonly List<CaseNoteExample> _examples = [];
    
    // System prompt for case note generation
    private const string SystemPrompt = @"You are an expert mental health professional assistant specializing in creating structured clinical case notes. Your task is to transform conversational therapy session transcripts into professional, well-organized SOAP (Subjective, Objective, Assessment, Plan) format case notes.

CRITICAL RULE: You must ONLY use information explicitly stated in the transcript. DO NOT infer, assume, or fabricate any clinical details.

Key guidelines:
1. Maintain professional, clinical language
2. Preserve all clinically relevant information from the transcript
3. Organize information into clear SOAP sections
4. If a SOAP section has no relevant information from the transcript, write ""Not documented in session"" or ""N/A""
5. Include safety assessments (SI/HI status) ONLY if mentioned in transcript
6. Note relevant history and context ONLY if mentioned in transcript
7. Be concise but comprehensive
8. Use standard clinical abbreviations appropriately
9. NEVER fabricate, infer, or assume information not explicitly stated in the transcript
10. If the transcript is brief or incomplete, reflect that in your note

Format your response using the following structure:
**SUBJECTIVE:**
[Patient's reported symptoms, concerns, and history - from transcript ONLY]

**OBJECTIVE:**
[Observable presentation, mental status exam findings - from transcript ONLY, or ""Not documented"" if not mentioned]

**ASSESSMENT:**
[Clinical impression, diagnosis, severity - based ONLY on what was stated in transcript, or ""Insufficient information documented"" if not enough detail]

**PLAN:**
[Treatment plan, interventions, follow-up - from transcript ONLY, or ""Not documented"" if not mentioned]";
    
    public TextGenerationService(ILogger<TextGenerationService> logger, IModelManager modelManager, string? examplesJsonPath = null)
    {
        _logger = logger;
        _modelManager = modelManager;
        
        // Load examples if path provided
        if (!string.IsNullOrEmpty(examplesJsonPath) && File.Exists(examplesJsonPath))
        {
            LoadExamples(examplesJsonPath);
        }
    }
    
    private void LoadExamples(string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var document = JsonDocument.Parse(json);
            
            if (document.RootElement.TryGetProperty("examples", out var examplesArray))
            {
                foreach (var example in examplesArray.EnumerateArray())
                {
                    var transcript = example.GetProperty("transcript").GetString();
                    var structuredNote = example.GetProperty("structuredNote").GetString();
                    
                    if (!string.IsNullOrEmpty(transcript) && !string.IsNullOrEmpty(structuredNote))
                    {
                        _examples.Add(new CaseNoteExample
                        {
                            Transcript = transcript,
                            StructuredNote = structuredNote
                        });
                    }
                }
            }
            
            _logger.LogInformation("Loaded {Count} case note examples for few-shot prompting", _examples.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load examples from {Path}", jsonPath);
        }
    }
    
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;
            
            _logger.LogInformation("Initializing text generation service with Phi-4-mini-instruct...");
            
            var modelPath = await _modelManager.GetTextGenerationModelPathAsync(cancellationToken);
            
            if (!Directory.Exists(modelPath))
            {
                throw new InvalidOperationException($"Model not found at {modelPath}. Please download models first.");
            }
            
            // Initialize ONNX Runtime GenAI Model
            _logger.LogInformation("Loading model from {Path}", modelPath);
            var config = new Config(modelPath);
            _model = new Model(config);
            _tokenizer = new Tokenizer(_model);
            // Note: Config is copied by Model constructor, safe to let it be collected
            
            _isInitialized = true;
            _logger.LogInformation("Text generation service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize text generation service");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }
    
    public async Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🤖 GENERATE START: Transcript length={Length}", request.TranscriptText?.Length ?? 0);
            _logger.LogInformation("Transcript: '{Text}'", request.TranscriptText);
            
            await EnsureInitializedAsync(cancellationToken);
            
            _logger.LogInformation("✅ EnsureInitializedAsync completed");
            
            if (_model == null || _tokenizer == null)
            {
                _logger.LogError("❌ Model or Tokenizer is NULL after initialization!");
                return new GenerationResult
                {
                    Success = false,
                    StructuredNote = string.Empty,
                    ErrorMessage = "Phi-3 model not initialized"
                };
            }
            
            _logger.LogInformation("Building prompt...");
            
            // Run generation on background thread to prevent blocking (Phi-3 takes 30-60 seconds)
            _logger.LogInformation("🔄 Starting Task.Run for generation...");
            var (generatedText, timedOut) = await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("📝 Building prompt...");
                    var prompt = BuildPrompt(request.TranscriptText ?? string.Empty);
                    _logger.LogInformation("✅ Prompt built, length={Length}", prompt.Length);
                    
                    _logger.LogInformation("🔤 Encoding prompt...");
                    var sequences = _tokenizer.Encode(prompt);
                    _logger.LogInformation("✅ Prompt encoded");
                    
                    _logger.LogInformation("⚙️ Creating generator params...");
                    using var generatorParams = new GeneratorParams(_model);
                    generatorParams.SetSearchOption("max_length", 2048);
                    generatorParams.SetSearchOption("temperature", 0.7);
                    generatorParams.SetSearchOption("top_p", 0.9);
                    _logger.LogInformation("✅ Generator params configured");
                    
                    _logger.LogInformation("🎰 Creating generator...");
                    using var generator = new Generator(_model, generatorParams);
                    _logger.LogInformation("✅ Generator created");
                    
                    _logger.LogInformation("🔄 Creating tokenizer stream...");
                    using var tokenizerStream = _tokenizer.CreateStream();
                    _logger.LogInformation("✅ Tokenizer stream created");
                    
                    _logger.LogInformation("📨 Appending token sequences...");
                    generator.AppendTokenSequences(sequences);
                    _logger.LogInformation("✅ Sequences appended");
                    
                    var textBuilder = new StringBuilder();
                    var generationStart = DateTime.UtcNow;
                    _logger.LogInformation("🚀 Starting generation loop...");
                    
                    int tokenCount = 0;
                    while (!generator.IsDone())
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogWarning("⚠️ Cancellation requested");
                            throw new OperationCanceledException();
                        }
                        
                        // Timeout protection (2 minutes)
                        if ((DateTime.UtcNow - generationStart).TotalMinutes > 2)
                        {
                            _logger.LogWarning("⏱️ Generation timeout after 2 minutes");
                            return (textBuilder.ToString().Trim(), true);
                        }
                        
                        generator.GenerateNextToken();
                        var nextToken = generator.GetNextTokens()[0];
                        var tokenText = tokenizerStream.Decode(nextToken);
                        
                        if (!string.IsNullOrEmpty(tokenText))
                        {
                            textBuilder.Append(tokenText);
                            tokenCount++;
                            if (tokenCount % 10 == 0)
                            {
                                _logger.LogInformation("  Generated {Count} tokens...", tokenCount);
                            }
                        }
                    }
                    
                    _logger.LogInformation("✅ Generation loop complete: {Total} tokens", tokenCount);
                    
                    var finalText = textBuilder.ToString().Trim();
                    _logger.LogInformation("📄 Final text length: {Length}", finalText.Length);
                    return (finalText, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 PHI-3 GENERATION CRASHED: {Message}", ex.Message);
                    _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                    throw;
                }
            }, cancellationToken);
            
            _logger.LogInformation("✅ Task.Run completed");
            
            var result = generatedText;
            
            _logger.LogInformation("Generated case note ({Length} chars)", result.Length);
            _logger.LogInformation("📄 GENERATED NOTE:\n{Note}", result);
            
            return new GenerationResult
            {
                Success = true,
                StructuredNote = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating case note");
            return new GenerationResult
            {
                Success = false,
                StructuredNote = string.Empty,
                ErrorMessage = ex.Message
            };
        }
    }

    
    public async IAsyncEnumerable<string> GenerateStreamAsync(GenerationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        var prompt = BuildPrompt(request.TranscriptText);
        
        // Encode the prompt
        var sequences = _tokenizer!.Encode(prompt);
        var inputTokens = sequences[0];
        
        // Configure generation parameters
        using var generatorParams = new GeneratorParams(_model!);
        generatorParams.SetSearchOption("max_length", (double)request.MaxLength);
        generatorParams.SetSearchOption("temperature", request.Temperature);
        generatorParams.SetSearchOption("top_p", 0.95);
        generatorParams.SetSearchOption("top_k", 50.0);
        
        // Create generator and tokenizer stream for incremental decoding
        using var generator = new Generator(_model!, generatorParams);
        using var tokenizerStream = _tokenizer.CreateStream();
        
        // Append initial token sequences
        generator.AppendTokenSequences(sequences);
        
        var generationStart = DateTime.UtcNow;
        
        while (!generator.IsDone())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            
            // Check timeout
            if ((DateTime.UtcNow - generationStart).TotalMinutes > 2)
            {
                _logger.LogWarning("Streaming generation timeout after 2 minutes");
                yield break;
            }
            
            generator.GenerateNextToken();
            
            // Decode and yield the next token
            var nextToken = generator.GetNextTokens()[0];
            var tokenText = tokenizerStream.Decode(nextToken);
            
            if (!string.IsNullOrEmpty(tokenText))
            {
                yield return tokenText;
            }
        }
    }
    
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            return _isInitialized;
        }
        catch
        {
            return false;
        }
    }
    
    private string BuildPrompt(string transcript)
    {
        var promptBuilder = new StringBuilder();
        
        // Add system prompt
        promptBuilder.AppendLine("<|system|>");
        promptBuilder.AppendLine(SystemPrompt);
        
        // Add few-shot examples
        if (_examples.Count > 0)
        {
            promptBuilder.AppendLine("\nHere are examples of well-formatted case notes:");
            
            foreach (var example in _examples.Take(3)) // Limit to 3 examples for context window
            {
                promptBuilder.AppendLine("\n<|user|>");
                promptBuilder.AppendLine($"Transcript: {example.Transcript}");
                promptBuilder.AppendLine("<|end|>");
                
                promptBuilder.AppendLine("<|assistant|>");
                promptBuilder.AppendLine(example.StructuredNote);
                promptBuilder.AppendLine("<|end|>");
            }
        }
        
        // Add the actual user transcript
        promptBuilder.AppendLine("\n<|user|>");
        promptBuilder.AppendLine($"Transcript: {transcript}");
        promptBuilder.AppendLine("<|end|>");
        promptBuilder.AppendLine("<|assistant|>");
        
        return promptBuilder.ToString();
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogInformation("Disposing text generation service");
        
        _tokenizer?.Dispose();
        _model?.Dispose();
        _initLock.Dispose();
        
        _disposed = true;
    }
}
