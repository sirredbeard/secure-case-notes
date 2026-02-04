using CaseNotes.Core.Models;

namespace CaseNotes.Core.Interfaces;

/// <summary>
/// Manages AI model downloads, caching, and lifecycle
/// </summary>
public interface IModelManager
{
    /// <summary>
    /// Gets the current status of all models
    /// </summary>
    Task<ModelStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Ensures all required models are downloaded and cached
    /// </summary>
    Task EnsureModelsDownloadedAsync(IProgress<ModelInfo>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the local path to the Sherpa-ONNX STT model directory
    /// </summary>
    Task<string> GetSherpaModelPathAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the local path to the Whisper model (redirects to Sherpa for compatibility)
    /// </summary>
    Task<string> GetWhisperModelPathAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the local path to the text generation model
    /// </summary>
    Task<string> GetTextGenerationModelPathAsync(CancellationToken cancellationToken = default);
}
