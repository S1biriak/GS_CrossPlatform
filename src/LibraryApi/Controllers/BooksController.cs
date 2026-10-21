using LibraryApi.Auth;
using LibraryApi.Dtos;
using LibraryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;

/// <summary>Каталог физических экземпляров книг.</summary>
[ApiController, Route("api/v1/books")]
public sealed class BooksController(CatalogService service) : ControllerBase
{
    /// <summary>Поиск, фильтрация и постраничный просмотр каталога.</summary>
    [HttpGet, AllowAnonymous]
    public Task<PagedResult<BookDto>> List([FromQuery] BookQuery query, CancellationToken ct) => service.Books(query, ct);

    [HttpGet("{id:int:min(1)}"), AllowAnonymous]
    public Task<BookDto> Get(int id, CancellationToken ct) => service.Book(id, ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<BookDto>> Create(CreateBookRequest request, CancellationToken ct)
    {
        var result = await service.CreateBook(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:int:min(1)}"), Authorize(Roles = Roles.Admin)]
    public Task<BookDto> Update(int id, UpdateBookRequest request, CancellationToken ct) => service.UpdateBook(id, request, ct);

    [HttpDelete("{id:int:min(1)}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteBook(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int:min(1)}/archive"), Authorize(Roles = Roles.Admin)]
    public Task<BookDto> Archive(int id, CancellationToken ct) => service.ArchiveBook(id, true, ct);

    [HttpPost("{id:int:min(1)}/restore"), Authorize(Roles = Roles.Admin)]
    public Task<BookDto> Restore(int id, CancellationToken ct) => service.ArchiveBook(id, false, ct);
}
