namespace PedeLocal.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>Validates and persists an uploaded file, returning its public relative URL
    /// (e.g. "/uploads/&lt;guid&gt;.jpg"). Throws AppException on any validation failure — never
    /// trusts the client-supplied file name or Content-Type alone (docs section 61).</summary>
    Task<string> SaveImageAsync(Stream content, string originalFileName, string declaredContentType, CancellationToken ct = default);
}
