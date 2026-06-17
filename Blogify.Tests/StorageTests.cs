using Blogify.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Blogify.Tests;

public sealed class StorageTests
{
    [Fact]
    public async Task LocalStorage_CompressesImageAndCreatesThumbnail()
    {
        await using TestEnvironment environment = new();
        ImageStorageProcessor processor = new(Options.Create(new StorageOptions()));
        LocalFileStorageService storage = new(environment, processor);

        await using MemoryStream input = CreateImageStream(800, 600);
        FormFile file = new(input, 0, input.Length, "file", "cover.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        Guid tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        string url = await storage.SaveAsync(file, tenantId);
        Assert.EndsWith(".webp", url);

        string filePath = Path.Combine(environment.WebRootPath!, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(filePath));

        (int Width, int Height)? dimensions = await storage.GetImageDimensionsAsync(url);
        Assert.Equal((800, 600), dimensions);

        string? thumbUrl = await storage.SaveThumbnailAsync(url, tenantId, 300);
        Assert.NotNull(thumbUrl);
        Assert.EndsWith("-thumb.webp", thumbUrl);

        string thumbPath = Path.Combine(environment.WebRootPath!, thumbUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(thumbPath));

        await storage.DeleteAsync(url);
        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(thumbPath));
    }

    [Fact]
    public async Task ImageProcessor_ReturnsWebpDimensions()
    {
        ImageStorageProcessor processor = new(Options.Create(new StorageOptions()));
        await using MemoryStream input = CreateImageStream(320, 240);
        FormFile file = new(input, 0, input.Length, "file", "cover.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        ProcessedImage image = await processor.CreateStoredImageAsync(file);

        await using MemoryStream output = new(image.Bytes);
        (int Width, int Height)? dimensions = await processor.GetDimensionsAsync(output);

        Assert.Equal((320, 240), dimensions);
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal(".webp", image.FileExtension);
    }

    private static MemoryStream CreateImageStream(int width, int height)
    {
        MemoryStream output = new();
        using Image<Rgba32> image = new(width, height);
        image.SaveAsPng(output);
        output.Position = 0;
        return output;
    }

    private sealed class TestEnvironment : IWebHostEnvironment, IAsyncDisposable
    {
        public TestEnvironment()
        {
            WebRootPath = Path.Combine(Path.GetTempPath(), "blogify-storage-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(WebRootPath);
        }

        public string ApplicationName { get; set; } = "Blogify.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; }
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = Environments.Development;

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(WebRootPath))
            {
                Directory.Delete(WebRootPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
