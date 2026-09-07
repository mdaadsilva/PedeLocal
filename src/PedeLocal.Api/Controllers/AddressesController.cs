using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Addresses;
using PedeLocal.Contracts.Addresses;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/me/addresses")]
[Authorize]
public class AddressesController(AddressService addressService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<AddressResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct) => Ok(await addressService.GetMineAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateAddressRequest request, CancellationToken ct)
    {
        var response = await addressService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetMine), response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, UpdateAddressRequest request, CancellationToken ct) =>
        Ok(await addressService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await addressService.DeleteAsync(id, ct);
        return NoContent();
    }
}
