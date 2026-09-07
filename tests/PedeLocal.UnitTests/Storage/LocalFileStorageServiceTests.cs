using Microsoft.Extensions.Configuration;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Infrastructure.Storage;
using Xunit;

namespace PedeLocal.UnitTests.Storage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "pedelocal-tests-" + Guid.NewGuid());

    private LocalFileStorageService BuildSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:LocalRootPath"] = _tempRoot,
                ["Storage:PublicBasePath"] = "/uploads"
            })
            .Build();
        return new LocalFileStorageService(config);
    }

    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x00, 0x00, 0x00, 0x01
    ];

    [Fact]
    public async Task SaveImage_ValidPng_ReturnsUrl_AndWritesFile()
    {
        var sut = BuildSut();
        using var stream = new MemoryStream(ValidPngBytes);

        var url = await sut.SaveImageAsync(stream, "foto.png", "image/png", CancellationToken.None);

        Assert.StartsWith("/uploads/", url);
        Assert.True(File.Exists(Path.Combine(_tempRoot, Path.GetFileName(url))));
    }

    [Fact]
    public async Task SaveImage_ContentDoesNotMatchExtension_Throws()
    {
        var sut = BuildSut();
        // A text file renamed to .png — magic bytes won't match.
        using var stream = new MemoryStream("not really a png"u8.ToArray());

        await Assert.ThrowsAsync<AppException>(() => sut.SaveImageAsync(stream, "fake.png", "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task SaveImage_DisallowedExtension_Throws()
    {
        var sut = BuildSut();
        using var stream = new MemoryStream(ValidPngBytes);

        await Assert.ThrowsAsync<AppException>(() => sut.SaveImageAsync(stream, "script.exe", "application/octet-stream", CancellationToken.None));
    }

    [Fact]
    public async Task SaveImage_EmptyFile_Throws()
    {
        var sut = BuildSut();
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<AppException>(() => sut.SaveImageAsync(stream, "empty.png", "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task SaveImage_OversizedFile_Throws()
    {
        var sut = BuildSut();
        var oversized = new byte[6 * 1024 * 1024];
        ValidPngBytes.CopyTo(oversized, 0);
        using var stream = new MemoryStream(oversized);

        await Assert.ThrowsAsync<AppException>(() => sut.SaveImageAsync(stream, "big.png", "image/png", CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
