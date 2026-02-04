using CaseNotes.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CaseNotes.Tests;

public class ModelManagerTests
{
    private readonly Mock<ILogger<ModelManager>> _loggerMock;
    private readonly ModelManager _modelManager;

    public ModelManagerTests()
    {
        _loggerMock = new Mock<ILogger<ModelManager>>();
        _modelManager = new ModelManager(_loggerMock.Object);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsModelStatus()
    {
        // Act
        var status = await _modelManager.GetStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.NotNull(status.WhisperModel);
        Assert.NotNull(status.TextGenerationModel);
        Assert.StartsWith("Sherpa", status.WhisperModel.Name);
        Assert.Equal("Phi-3-mini-4k-instruct", status.TextGenerationModel.Name);
    }

    [Fact]
    public async Task GetWhisperModelPathAsync_ReturnsValidPath()
    {
        // Act
        var path = await _modelManager.GetWhisperModelPathAsync();

        // Assert
        Assert.NotNull(path);
        Assert.NotEmpty(path);
        Assert.Contains("whisper", path.ToLowerInvariant());
    }

    [Fact]
    public async Task GetTextGenerationModelPathAsync_ReturnsValidPath()
    {
        // Act
        var path = await _modelManager.GetTextGenerationModelPathAsync();

        // Assert
        Assert.NotNull(path);
        Assert.NotEmpty(path);
        Assert.Contains("phi", path.ToLowerInvariant());
    }

    [Fact]
    public async Task ModelPaths_AreOSSpecific()
    {
        // Act
        var whisperPath = await _modelManager.GetWhisperModelPathAsync();
        var phi4Path = await _modelManager.GetTextGenerationModelPathAsync();

        // Assert
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("LocalAppData", whisperPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SecureCaseNotes", whisperPath);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Contains("Library/Application Support", whisperPath);
            Assert.Contains("SecureCaseNotes", whisperPath);
        }
    }
}
