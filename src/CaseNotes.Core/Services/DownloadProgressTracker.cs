using CaseNotes.Core.Models;

namespace CaseNotes.Core.Services;

/// <summary>
/// Tracks real-time download progress across multiple models
/// Thread-safe singleton for sharing progress between background task and API endpoints
/// </summary>
public sealed class DownloadProgressTracker
{
    private readonly object _lock = new();
    private double _whisperProgress = 0;
    private double _phi4Progress = 0;
    private bool _isComplete = false;
    
    public double WhisperProgress
    {
        get { lock (_lock) return _whisperProgress; }
    }
    
    public double Phi4Progress
    {
        get { lock (_lock) return _phi4Progress; }
    }
    
    public bool IsComplete
    {
        get { lock (_lock) return _isComplete; }
    }
    
    public void UpdateProgress(ModelInfo modelInfo)
    {
        lock (_lock)
        {
            if (modelInfo.Name.StartsWith("Sherpa", StringComparison.OrdinalIgnoreCase))
            {
                _whisperProgress = modelInfo.DownloadProgress;
            }
            else if (modelInfo.Name.Contains("Phi", StringComparison.OrdinalIgnoreCase))
            {
                _phi4Progress = modelInfo.DownloadProgress;
            }
        }
    }
    
    public void MarkComplete()
    {
        lock (_lock)
        {
            _isComplete = true;
            _whisperProgress = 100;
            _phi4Progress = 100;
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _whisperProgress = 0;
            _phi4Progress = 0;
            _isComplete = false;
        }
    }
}
