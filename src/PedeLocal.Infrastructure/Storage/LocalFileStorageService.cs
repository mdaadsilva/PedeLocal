using Microsoft.Extensions.Configuration;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.Storage;

/// <summary>Local-disk storage for uploaded images. Never trusts the client-declared file name
/// or Content-Type: validates size, extension, and the file's actual magic bytes before writing
/// under a server-generated name (docs section 61). Swappable later for S3/Blob storage — only
/// this class and its DI registration would change.</summary>
public class LocalFileStorageService : IFileStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, byte[]> MagicBytesBySignature = new()
    {
        [".jpg"] = [0xFF, 0xD8, 0xFF],
        [".png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        [".webp"] = "RIFF"u8.ToArray() // followed by "WEBP" at offset 8, checked separately.
    };

    private readonly string _rootPath;
    private readonly string _publicBasePath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _rootPath = configuration["Storage:LocalRootPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        _publicBasePath = configuration["Storage:PublicBasePath"] ?? "/uploads";
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveImageAsync(Stream content, string originalFileName, string declaredContentType, CancellationToken ct = default)
    {
        if (content.Length == 0)
        {
            throw new AppException("EMPTY_FILE", "Arquivo vazio.");
        }
        if (content.Length > MaxFileSizeBytes)
        {
            throw new AppException("FILE_TOO_LARGE", $"Arquivo maior que o limite de {MaxFileSizeBytes / 1024 / 1024}MB.");
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            throw new AppException("INVALID_FILE_TYPE", "Apenas imagens JPG, PNG ou WEBP são permitidas.");
        }

        var header = new byte[16];
        var read = await content.ReadAsync(header, 0, header.Length, ct);
        content.Position = 0;

        var normalizedExtension = extension == ".jpeg" ? ".jpg" : extension;
        if (!MatchesMagicBytes(header, read, normalizedExtension))
        {
            throw new AppException("INVALID_FILE_TYPE", "O conteúdo do arquivo não corresponde a uma imagem válida.");
        }

        // Server-generated name — the client's original file name is never used for the path,
        // which also rules out path traversal / injection via the file name.
        var fileName = $"{Guid.NewGuid():N}{normalizedExtension}";
        var fullPath = Path.Combine(_rootPath, fileName);

        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return $"{_publicBasePath}/{fileName}";
    }

    private static bool MatchesMagicBytes(byte[] header, int bytesRead, string extension)
    {
        if (extension == ".webp")
        {
            return bytesRead >= 12 &&
                   header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                   header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        }

        var signature = MagicBytesBySignature[extension];
        return bytesRead >= signature.Length && header.AsSpan(0, signature.Length).SequenceEqual(signature);
    }
}
