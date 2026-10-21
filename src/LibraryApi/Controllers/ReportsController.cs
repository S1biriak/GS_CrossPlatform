using LibraryApi.Dtos;
using LibraryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;

/// <summary>Четыре отчёта, объединяющие связанные сущности через LINQ.</summary>
[ApiController, Route("api/v1/reports"), Authorize]
public sealed class ReportsController(ReportService service) : ControllerBase
{
    [HttpGet("active-loans")]
    public Task<PagedResult<ActiveLoanDto>> Active([FromQuery] PageQuery query, CancellationToken ct) => service.Active(query, ct);

    [HttpGet("overdue-loans")]
    public Task<PagedResult<OverdueLoanDto>> Overdue([FromQuery] PageQuery query, CancellationToken ct) => service.Overdue(query, ct);

    /// <summary>Рейтинг физических экземпляров по числу выдач; from включительно, to исключительно.</summary>
    [HttpGet("popular-books")]
    public Task<PagedResult<PopularBookDto>> Popular([FromQuery] ReportQuery query, CancellationToken ct) => service.Popular(query, ct);

    /// <summary>Читатели, включая не имеющих выдач. Период фильтрует дату выдачи.</summary>
    [HttpGet("reader-statistics")]
    public Task<PagedResult<ReaderStatisticsDto>> Readers([FromQuery] ReportQuery query, CancellationToken ct) => service.Readers(query, ct);
}
