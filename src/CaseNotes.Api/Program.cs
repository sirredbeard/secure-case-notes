using System.Diagnostics;
using System.Runtime.InteropServices;
using CaseNotes.Core.Interfaces;
using CaseNotes.Core.Services;
using Serilog;

// P/Invoke for hiding console window on Windows
const int SW_HIDE = 0;

[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

// Check for debug mode
var isDebugMode = args.Contains("--debug");

// Make debug mode available globally
AppDomain.CurrentDomain.SetData("IsDebugMode", isDebugMode);

// Hide console window on Windows unless debug mode
if (!isDebugMode && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    var handle = GetConsoleWindow();
    ShowWindow(handle, SW_HIDE);
}

// Configure Serilog - VERBOSE logging to CONSOLE ONLY (no disk writes for HIPAA)
// PHI will appear in console when --debug is enabled, but never persisted to disk
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information();

// ONLY log to console in debug mode - NEVER write PHI to disk
if (isDebugMode)
{
    logConfig.WriteTo.Console();
}

Log.Logger = logConfig.CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Configure Kestrel for localhost:5000
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenLocalhost(5000);
    });

    // Disable telemetry for HIPAA compliance
    Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
    Environment.SetEnvironmentVariable("ONNXRUNTIME_TELEMETRY_OPTOUT", "1");

    // Add services
    builder.Services.AddOpenApi();
    
    // Configure SignalR with increased message size for audio recordings
    // Real-world: 10-20 minute recordings can be 10-50 MB
    builder.Services.AddSignalR(options =>
    {
        options.MaximumReceiveMessageSize = 100 * 1024 * 1024; // 100 MB
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(5); // Phi-3 generation timeout (increased)
        options.HandshakeTimeout = TimeSpan.FromMinutes(1);
        options.KeepAliveInterval = TimeSpan.FromSeconds(10); // Send pings to keep alive
        options.EnableDetailedErrors = isDebugMode; // Detailed errors in debug mode
    });
    
    // Configure Blazor
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents(options =>
        {
            options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
        });
    
    // Configure Circuit options for long-running operations (Phi-3 generation)
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10); // Increased for multi-note sessions
        options.DisconnectedCircuitMaxRetained = 100;
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
    });
    
    // Configure HttpClient with base address and extended timeouts for Blazor components
    builder.Services.AddScoped(sp => new HttpClient 
    { 
        BaseAddress = new Uri("http://localhost:5000"),
        Timeout = TimeSpan.FromMinutes(5) // Allow time for transcription + generation
    });
    
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Register services
    builder.Services.AddSingleton<IModelManager, ModelManager>();
    
    // Shared progress tracker for model downloads
    var downloadProgress = new DownloadProgressTracker();
    builder.Services.AddSingleton(downloadProgress);
    
    // Add request timeout services for long Phi-3 operations
    builder.Services.AddRequestTimeouts(options =>
    {
        options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
    });
    
    // Get the examples.json path
    var examplesPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "examples.json");
    builder.Services.AddSingleton<ITextGenerationService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<TextGenerationService>>();
        var modelManager = sp.GetRequiredService<IModelManager>();
        return new TextGenerationService(logger, modelManager, examplesPath);
    });
    
    builder.Services.AddSingleton<ITranscriptionService, TranscriptionService>();

    var app = builder.Build();

    // Start downloading models in the background with progress reporting
    _ = Task.Run(async () =>
    {
        try
        {
            var modelManager = app.Services.GetRequiredService<IModelManager>();
            var progressTracker = app.Services.GetRequiredService<DownloadProgressTracker>();
            
            var progress = new Progress<CaseNotes.Core.Models.ModelInfo>(info =>
            {
                progressTracker.UpdateProgress(info);
                Log.Information("Download progress: {Model} - {Progress}%", info.Name, info.DownloadProgress);
            });
            
            await modelManager.EnsureModelsDownloadedAsync(progress, cancellationToken: default);
            progressTracker.MarkComplete();
            Log.Information("All models downloaded successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error downloading models");
        }
    });

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    
    // Add request timeout middleware for long-running Phi-3 operations
    app.UseRequestTimeouts();

    app.UseCors();
    app.UseStaticFiles();
    app.UseAntiforgery();

    // Map Razor Components
    app.MapRazorComponents<CaseNotes.Api.Components.App>()
        .AddInteractiveServerRenderMode();

    // Health check
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

    // Model status endpoint with live progress
    app.MapGet("/api/models/status", async (
        IModelManager modelManager, 
        DownloadProgressTracker progressTracker, 
        CancellationToken ct) =>
    {
        var status = await modelManager.GetStatusAsync(ct);
        
        // Merge with live download progress
        if (progressTracker.WhisperProgress > 0)
        {
            status.WhisperModel!.DownloadProgress = progressTracker.WhisperProgress;
            status.WhisperModel.IsDownloaded = progressTracker.WhisperProgress >= 100;
        }
        
        if (progressTracker.Phi4Progress > 0)
        {
            status.TextGenerationModel!.DownloadProgress = progressTracker.Phi4Progress;
            status.TextGenerationModel.IsDownloaded = progressTracker.Phi4Progress >= 100;
        }
        
        status.AllModelsReady = progressTracker.IsComplete || 
            (status.WhisperModel!.IsDownloaded && status.TextGenerationModel!.IsDownloaded);
        
        return Results.Ok(status);
    });

    // Transcription endpoint
    app.MapPost("/api/transcribe", async (
        IFormFile audio,
        ITranscriptionService transcriptionService,
        CancellationToken ct) =>
    {
        try
        {
            if (audio == null || audio.Length == 0)
            {
                Log.Warning("Transcribe called with no audio");
                return Results.BadRequest(new { error = "No audio file provided" });
            }

            Log.Information("Transcribe endpoint called with {Size} bytes", audio.Length);

            // Read audio data into memory (HIPAA: no disk writes)
            using var memoryStream = new MemoryStream();
            await audio.CopyToAsync(memoryStream, ct);
            var audioData = memoryStream.ToArray();

            var request = new CaseNotes.Core.Models.TranscriptionRequest
            {
                AudioData = audioData,
                AudioFormat = "wav",
                SampleRate = 16000
            };

            var result = await transcriptionService.TranscribeAsync(request, ct);
            
            Log.Information("Transcription result: Success={Success}, TextLength={Length}, Error={Error}",
                result.Success, result.Text?.Length ?? 0, result.ErrorMessage);
            
            if (!result.Success)
            {
                Log.Error("Transcription failed: {Error}", result.ErrorMessage);
                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            return Results.Ok(new { text = result.Text });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CRITICAL: Transcription endpoint crashed");
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    })
    .DisableAntiforgery()
    .WithRequestTimeout(TimeSpan.FromMinutes(2)); // Sherpa can take time on large files

    // Generation endpoint with extended timeout for Phi-3 (30-60 seconds)
    app.MapPost("/api/generate", async (
        CaseNotes.Core.Models.GenerationRequest request,
        ITextGenerationService generationService,
        CancellationToken ct) =>
    {
        try
        {
            Log.Information("Generation endpoint called with transcript length: {Length}", request.TranscriptText?.Length ?? 0);
            
            var result = await generationService.GenerateAsync(request, ct);
            
            Log.Information("Generation result: Success={Success}, NoteLength={Length}, Error={Error}", 
                result.Success, result.StructuredNote?.Length ?? 0, result.ErrorMessage);
            
            if (!result.Success)
            {
                Log.Error("Generation failed: {Error}", result.ErrorMessage);
                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            return Results.Ok(new { structuredNote = result.StructuredNote });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CRITICAL: Generation endpoint crashed");
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    })
    .WithRequestTimeout(TimeSpan.FromMinutes(3)); // Allow time for Phi-3 CPU inference

    // Start browser after app is ready (unless in debug mode)
    if (!isDebugMode)
    {
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(() =>
            {
                // Wait a moment for the server to be ready
                Thread.Sleep(1000);
                OpenBrowser("http://localhost:5000");
            });
        });
    }

    if (isDebugMode)
    {
        Log.Information("Starting Secure Case Notes in DEBUG mode on http://localhost:5000");
        Log.Information("Console logging enabled. Browser will NOT auto-open.");
    }
    else
    {
        Log.Information("Starting Secure Case Notes on http://localhost:5000");
    }
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void OpenBrowser(string url)
{
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
            // Linux
            Process.Start("xdg-open", url);
        }
        Log.Information("Opened browser to {Url}", url);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to automatically open browser. Please navigate to {Url}", url);
    }
}
