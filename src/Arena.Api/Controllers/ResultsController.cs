using Arena.Application.Features.Events;
using Arena.Application.Features.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

/// <summary>Match results and player statistics. Routes live under the event they belong to.</summary>
[ApiController, Route("api/v1/events")]
public sealed class ResultsController(ResultService results) : ArenaController
{
    [HttpPut("{id:int}/result"), Authorize(Roles = Management)]
    public Task<EventDto> Result(int id, ResultRequest request, CancellationToken ct) =>
        results.ApproveAsync(id, request, ActorId, ct);

    [HttpGet("{id:int}/result-history"), Authorize(Roles = Management)]
    public Task<PageResult<ResultAudit>> ResultHistory(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        results.HistoryAsync(id, page, pageSize, ct);

    [HttpPut("{id:int}/player-stats"), Authorize(Roles = Management)]
    public Task<MatchPlayerStat> Stats(int id, PlayerStatRequest request, CancellationToken ct) =>
        results.SetPlayerStatAsync(id, request, ct);

    [HttpGet("{id:int}/player-stats")]
    public Task<PageResult<MatchPlayerStat>> Stats(int id, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        results.PlayerStatsAsync(id, page, pageSize, ct);
}
