using LibraryApi.Auth;
using LibraryApi.Dtos;
using LibraryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;

[ApiController, Route("api/v1/readers"), Authorize]
public sealed class ReadersController(CatalogService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ReaderDto>> List([FromQuery] ReaderQuery query, CancellationToken ct) => service.Readers(query, ct);

    [HttpGet("{id:int:min(1)}")]
    public Task<ReaderDto> Get(int id, CancellationToken ct) => service.Reader(id, ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ReaderDto>> Create(CreateReaderRequest request, CancellationToken ct)
    {
        var result = await service.CreateReader(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:int:min(1)}"), Authorize(Roles = Roles.Admin)]
    public Task<ReaderDto> Update(int id, UpdateReaderRequest request, CancellationToken ct) => service.UpdateReader(id, request, ct);

    [HttpDelete("{id:int:min(1)}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteReader(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int:min(1)}/block"), Authorize(Roles = Roles.Admin)]
    public Task<ReaderDto> Block(int id, CancellationToken ct) => service.BlockReader(id, true, ct);

    [HttpPost("{id:int:min(1)}/unblock"), Authorize(Roles = Roles.Admin)]
    public Task<ReaderDto> Unblock(int id, CancellationToken ct) => service.BlockReader(id, false, ct);
}
