using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Uploads;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
[EnableRateLimiting("auth")]
public class UploadsController(IFileStorageService fileStorageService) : ControllerBase
{
    [HttpPost("image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadImageResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new AppException("FILE_REQUIRED", "Nenhum arquivo enviado.");
        }

        await using var stream = file.OpenReadStream();
        var url = await fileStorageService.SaveImageAsync(stream, file.FileName, file.ContentType, ct);
        return Created(url, new UploadImageResponse(url));
    }
}
