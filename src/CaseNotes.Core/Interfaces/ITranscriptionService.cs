using CaseNotes.Core.Models;

namespace CaseNotes.Core.Interfaces;

/// <summary>
/// Service for transcribing audio to text using Whisper
/// </summary>
public interface ITranscriptionService : IDisposable
{
    /// <summary>
    /// Transcribes audio data to text
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the transcription service is ready
    /// </summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}
