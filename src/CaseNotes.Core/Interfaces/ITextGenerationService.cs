using CaseNotes.Core.Models;

namespace CaseNotes.Core.Interfaces;

/// <summary>
/// Service for generating structured case notes from transcripts
/// </summary>
public interface ITextGenerationService : IDisposable
{
    /// <summary>
    /// Generates a structured case note from a transcript
    /// </summary>
    Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a structured case note with streaming support
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamAsync(GenerationRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the generation service is ready
    /// </summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}
