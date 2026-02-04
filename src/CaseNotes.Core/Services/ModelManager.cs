using System.Runtime.InteropServices;
using CaseNotes.Core.Interfaces;
using CaseNotes.Core.Models;
using Microsoft.Extensions.Logging;
using HuggingfaceHub;

namespace CaseNotes.Core.Services;

/// <summary>
/// Manages AI model downloads, caching, and storage using HuggingfaceHub
/// Implements HIPAA-compliant model caching with OS-specific locations
/// </summary>
public sealed class ModelManager(ILogger<ModelManager> logger) : IModelManager
{
    private readonly ILogger<ModelManager> _logger = logger;
    
    // Model configurations
    private static readonly Core.Models.ModelInfo SherpaModelInfo = new()
    {
        Name = "Sherpa-Zipformer-En-2023-06-26",
        HuggingFaceRepo = "", // Downloaded from GitHub, not HuggingFace
        Variant = "",
        EstimatedSizeBytes = 76_000_000, // ~76MB (int8 quantized)
        IsDownloaded = false
    };
    
    private static readonly Core.Models.ModelInfo Phi4ModelInfo = new()
    {
        Name = "Phi-3-mini-4k-instruct",
        HuggingFaceRepo = "microsoft/Phi-3-mini-4k-instruct-onnx",
        Variant = "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4",
        EstimatedSizeBytes = 2_500_000_000, // ~2.5GB for int4 quantized
        IsDownloaded = false
    };
    
    /// <summary>
    /// Gets the OS-specific cache directory for models
    /// Windows: %LOCALAPPDATA%\SecureCaseNotes\models
    /// macOS: ~/Library/Application Support/SecureCaseNotes/models
    /// </summary>
    private static string GetModelCacheDirectory()
    {
        string baseDir;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecureCaseNotes",
                "models");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SecureCaseNotes",
                "models");
        }
        else
        {
            // Linux fallback
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDir = Path.Combine(home, ".local", "share", "SecureCaseNotes", "models");
        }
        
        return baseDir;
    }
    
    public async Task<ModelStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        // Check if models are downloaded
        var sherpaDownloaded = await IsModelDownloadedAsync(SherpaModelInfo, cancellationToken);
        var phi4Downloaded = await IsModelDownloadedAsync(Phi4ModelInfo, cancellationToken);
        
        SherpaModelInfo.IsDownloaded = sherpaDownloaded;
        Phi4ModelInfo.IsDownloaded = phi4Downloaded;
        
        return new ModelStatus
        {
            WhisperModel = new Core.Models.ModelInfo 
            { 
                Name = SherpaModelInfo.Name,
                HuggingFaceRepo = SherpaModelInfo.HuggingFaceRepo,
                Variant = SherpaModelInfo.Variant,
                EstimatedSizeBytes = SherpaModelInfo.EstimatedSizeBytes,
                IsDownloaded = sherpaDownloaded,
                DownloadProgress = sherpaDownloaded ? 100.0 : 0.0
            },
            TextGenerationModel = new Core.Models.ModelInfo 
            { 
                Name = Phi4ModelInfo.Name,
                HuggingFaceRepo = Phi4ModelInfo.HuggingFaceRepo,
                Variant = Phi4ModelInfo.Variant,
                EstimatedSizeBytes = Phi4ModelInfo.EstimatedSizeBytes,
                IsDownloaded = phi4Downloaded,
                DownloadProgress = phi4Downloaded ? 100.0 : 0.0
            },
            AllModelsReady = sherpaDownloaded && phi4Downloaded
        };
    }
    
    private async Task<bool> IsModelDownloadedAsync(Core.Models.ModelInfo modelInfo, CancellationToken cancellationToken)
    {
        var path = modelInfo.Name.StartsWith("Sherpa")
            ? await GetSherpaModelPathAsync(cancellationToken)
            : await GetTextGenerationModelPathAsync(cancellationToken);
        
        // Check if directory exists and has required files
        if (!Directory.Exists(path))
            return false;
        
        // For Sherpa, check for specific required files
        if (modelInfo.Name.StartsWith("Sherpa"))
        {
            var requiredFiles = new[] { 
                "encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx", 
                "decoder-epoch-99-avg-1-chunk-16-left-128.onnx", 
                "joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx", 
                "tokens.txt" 
            };
            return requiredFiles.All(f => File.Exists(Path.Combine(path, f)));
        }
        
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        return files.Length > 0;
    }
    
    public async Task EnsureModelsDownloadedAsync(IProgress<Core.Models.ModelInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await GetStatusAsync(cancellationToken);
            
            if (!status.WhisperModel!.IsDownloaded)
            {
                _logger.LogInformation("Downloading Sherpa-ONNX model...");
                await DownloadSherpaModelAsync(SherpaModelInfo, progress, cancellationToken);
            }
            
            if (!status.TextGenerationModel!.IsDownloaded)
            {
                _logger.LogInformation("Downloading Phi-4 model...");
                await DownloadModelAsync(Phi4ModelInfo, progress, cancellationToken);
            }
            
            _logger.LogInformation("All models are ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading models");
            throw;
        }
    }
    
    public Task<string> GetSherpaModelPathAsync(CancellationToken cancellationToken = default)
    {
        var basePath = GetModelCacheDirectory();
        // Sherpa-ONNX model directory
        var modelPath = Path.Combine(basePath, "sherpa-onnx-streaming-zipformer-en-2023-06-26");
        return Task.FromResult(modelPath);
    }
    
    public Task<string> GetWhisperModelPathAsync(CancellationToken cancellationToken = default)
    {
        // Redirect to Sherpa model path for backward compatibility
        return GetSherpaModelPathAsync(cancellationToken);
    }
    
    public Task<string> GetTextGenerationModelPathAsync(CancellationToken cancellationToken = default)
    {
        var basePath = GetModelCacheDirectory();
        // Point to the actual model variant subdirectory where genai_config.json exists
        var modelPath = Path.Combine(basePath, "phi-3-mini-4k-instruct-onnx", "cpu_and_mobile", "cpu-int4-rtn-block-32");
        return Task.FromResult(modelPath);
    }
    
    
    private async Task DownloadSherpaModelAsync(Core.Models.ModelInfo modelInfo, IProgress<Core.Models.ModelInfo>? progress, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting download for Sherpa-ONNX model");
            
            var localPath = await GetSherpaModelPathAsync(cancellationToken);
            Directory.CreateDirectory(localPath);
            
            // Sherpa model URL on GitHub releases
            const string modelUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2";
            const string archiveName = "sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2";
            var archivePath = Path.Combine(Path.GetTempPath(), archiveName);
            
            // Download the archive
            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            {
                _logger.LogInformation("Downloading from {Url}...", modelUrl);
                
                using var response = await httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? modelInfo.EstimatedSizeBytes;
                long downloadedBytes = 0;
                
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                
                var buffer = new byte[81920];
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    downloadedBytes += bytesRead;
                    
                    var progressPercent = (int)((downloadedBytes * 100) / totalBytes);
                    modelInfo.DownloadProgress = progressPercent;
                    progress?.Report(modelInfo);
                }
            }
            
            _logger.LogInformation("Extracting archive...");
            
            // Extract the archive
            await ExtractTarBz2Async(archivePath, Path.GetDirectoryName(localPath)!, cancellationToken);
            
            // Clean up archive
            File.Delete(archivePath);
            
            modelInfo.IsDownloaded = true;
            modelInfo.DownloadProgress = 100.0;
            progress?.Report(modelInfo);
            
            _logger.LogInformation("Sherpa-ONNX model downloaded successfully to {Path}", localPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download Sherpa model");
            throw;
        }
    }
    
    private async Task ExtractTarBz2Async(string archivePath, string destinationPath, CancellationToken cancellationToken)
    {
        // For now, use a simple approach - on Windows we can use System.Formats.Tar (NET 7+)
        // and SharpCompress for BZ2
        
        try
        {
            // Use tar command if available (works on modern Windows, macOS, Linux)
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tar.exe" : "tar",
                Arguments = $"-xjf \"{archivePath}\" -C \"{destinationPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start tar process");
            
            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"tar extraction failed: {error}");
            }
            
            _logger.LogInformation("Extracted archive successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract tar.bz2 archive");
            throw;
        }
    }
    
    private async Task DownloadModelAsync(Core.Models.ModelInfo modelInfo, IProgress<Core.Models.ModelInfo>? progress, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting download for {ModelName} from {Repo}", modelInfo.Name, modelInfo.HuggingFaceRepo);
            
            var localPath = await GetTextGenerationModelPathAsync(cancellationToken);
            
            // Use HuggingfaceHub to download both models
            var progressHandler = new ModelDownloadProgress(modelInfo, progress, _logger);
            
            _logger.LogInformation("Downloading snapshot from {Repo} to {Path}...", modelInfo.HuggingFaceRepo, localPath);
            
            var snapshotPath = await HFDownloader.DownloadSnapshotAsync(
                modelInfo.HuggingFaceRepo,
                localDir: localPath,
                progress: progressHandler
            );
            
            _logger.LogInformation("Downloaded {ModelName} to {Path}", modelInfo.Name, snapshotPath);
            
            modelInfo.IsDownloaded = true;
            modelInfo.DownloadProgress = 100.0;
            progress?.Report(modelInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {ModelName}", modelInfo.Name);
            throw;
        }
    }
    
    // Progress handler for HuggingfaceHub downloads with size-weighted calculation
    private class ModelDownloadProgress(Core.Models.ModelInfo modelInfo, IProgress<Core.Models.ModelInfo>? parentProgress, ILogger logger) : IGroupedProgress
    {
        private readonly Dictionary<string, int> _fileProgress = new();
        private int _lastReportedProgress = 0;
        
        // File size weights (in MB) based on known model structures
        private static readonly Dictionary<string, long> FileWeights = new()
        {
            // Phi-3 files (total ~2.5GB)
            ["genai_config.json"] = 1,                           // ~2 KB
            [".onnx.data"] = 2000,                               // ~2 GB (largest file)
            [".onnx"] = 500,                                     // ~500 MB
            ["tokenizer.json"] = 2,                              // ~2 MB
            ["tokenizer_config.json"] = 1,                       // ~1 KB
            
            // Whisper ONNX model files (total ~75MB)
            ["config.json"] = 1,                                 // ~2 KB
            ["preprocessor_config.json"] = 1,                    // ~1 KB
            ["vocabulary.json"] = 1,                             // ~800 KB
            ["encoder_model.onnx"] = 30,                         // ~30 MB
            ["decoder_model_merged.onnx"] = 40,                  // ~40 MB
            
            // Default for unknown files
            ["_default"] = 10
        };
        
        public void Report(string filename, int fileProgress)
        {
            _fileProgress[filename] = fileProgress;
            
            // Calculate weighted progress based on file sizes
            var overallProgress = CalculateWeightedProgress();
            
            // Only report if progress changed significantly (every 5%)
            if (overallProgress >= _lastReportedProgress + 5 || overallProgress >= 100)
            {
                _lastReportedProgress = overallProgress;
                modelInfo.DownloadProgress = overallProgress;
                parentProgress?.Report(modelInfo);
                
                logger.LogInformation("{Model}: {Progress}% - {File}", 
                    modelInfo.Name, overallProgress, filename);
            }
        }
        
        private int CalculateWeightedProgress()
        {
            if (_fileProgress.Count == 0)
                return 0;
            
            long totalWeightedProgress = 0;
            long totalWeight = 0;
            
            foreach (var (filename, progress) in _fileProgress)
            {
                var weight = GetFileWeight(filename);
                totalWeightedProgress += progress * weight;
                totalWeight += weight;
            }
            
            return totalWeight > 0 ? (int)(totalWeightedProgress / totalWeight) : 0;
        }
        
        private static long GetFileWeight(string filename)
        {
            // Try exact match first
            if (FileWeights.TryGetValue(filename, out var exactWeight))
                return exactWeight;
            
            // Try pattern match (e.g., ".onnx.data" matches any file ending with that)
            foreach (var (pattern, weight) in FileWeights)
            {
                if (pattern.StartsWith('.') && filename.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    return weight;
                if (filename.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return weight;
            }
            
            // Default weight
            return FileWeights["_default"];
        }
    }
}
