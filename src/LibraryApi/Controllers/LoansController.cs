using LibraryApi.Auth;
using LibraryApi.Dtos;
using LibraryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;

[ApiController, Route("api/v1/loans"), Authorize]
public sealed class LoansController(LoanService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<LoanDto>> List([FromQuery] LoanQuery query, CancellationToken ct) => service.List(query, ct);

    [HttpGet("{id:int:min(1)}")]
    public Task<LoanDto> Get(int id, CancellationToken ct) => service.Get(id, ct);

    /// <summary>Выдать свободный экземпляр. По умолчанию срок составляет 14 дней.</summary>
    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LoanDto>> Create(CreateLoanRequest request, CancellationToken ct)
    {
        var result = await service.Create(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Изменить срок активной выдачи. Книга и читатель остаются прежними.</summary>
    [HttpPut("{id:int:min(1)}"), Authorize(Roles = Roles.Admin)]
    public Task<LoanDto> Update(int id, UpdateLoanRequest request, CancellationToken ct) => service.Update(id, request, ct);

    /// <summary>Удалить завершённую выдачу (учебная операция исправления истории).</summary>
    [HttpDelete("{id:int:min(1)}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.Delete(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int:min(1)}/return"), Authorize(Roles = Roles.Admin)]
    public Task<LoanDto> Return(int id, CancellationToken ct) => service.Return(id, ct);
}
