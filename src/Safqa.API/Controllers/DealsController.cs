using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
using Safqa.Infrastructure;
namespace Safqa.API.Controllers;

[ApiController, Route("api/v1/deals"), Authorize]
public class DealsController(SafqaDbContext db, ICurrentUser me, IMediator mediator, IHubContext<NotificationsHub> hub) : ControllerBase
{
    [HttpGet("board")]
    public async Task<BoardDto> Board(CancellationToken ct)
    {
        var org = me.OrganizationId; var since = DateTime.UtcNow.AddDays(-30);
        var stages = await db.Stages.Where(s => s.OrganizationId == org && s.IsActive).OrderBy(s => s.Position)
            .Select(s => new StageDto(s.Id, s.Name, s.NameAr, s.Position, s.Kind, s.DefaultProbability)).ToListAsync(ct);
        var deals = await (from d in db.Deals.ScopedTo(me).Where(d => d.Status == DealStatus.Open || d.ClosedAt >= since)
                           join c in db.Companies on d.CompanyId equals c.Id into cj
                           from c in cj.DefaultIfEmpty()
                           orderby d.Amount descending
                           select new DealDto(d.Id, d.Number, d.Name, d.Amount, d.Currency, d.StageId, d.Probability, d.Version, c!.Name)).ToListAsync(ct);
        return new BoardDto(stages, deals);
    }

    [HttpPost("{id:guid}/move-stage")]
    public async Task<MoveResult> MoveStage(Guid id, MoveStageRequest r, CancellationToken ct)
    {
        var d = await mediator.Send(new MoveDealStageCommand(id, r.StageId, r.Version, r.Won, r.Lost), ct);
        await hub.Clients.Group(me.OrganizationId.ToString()).SendAsync("dealMoved", new { d.Id, d.Name, d.StageId, by = me.UserId }, ct);
        return new MoveResult(d.Id, d.Version, d.StageId, d.Probability, d.Status);
    }

    /// History timeline for the deal details sheet.
    [HttpGet("{id:guid}/activities")]
    public async Task<List<ActivityLogDto>> Activities(Guid id, CancellationToken ct)
    {
        if (!await db.Deals.ScopedTo(me).AnyAsync(d => d.Id == id, ct)) throw new NotFoundException("Deal not found");
        return await (from a in db.ActivityLogs where a.EntityType == "Deal" && a.EntityId == id
                      join p in db.Profiles on a.ActorId equals p.Id into pj
                      from p in pj.DefaultIfEmpty()
                      orderby a.Timestamp descending
                      select new ActivityLogDto(a.Id, a.Action, a.Description, p!.FullName, a.Timestamp)).ToListAsync(ct);
    }

    [HttpGet("export"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var rows = await (from d in db.Deals.ScopedTo(me)
                          join s in db.Stages on d.StageId equals s.Id
                          join c in db.Companies on d.CompanyId equals c.Id into cj
                          from c in cj.DefaultIfEmpty()
                          orderby d.CreatedAt descending
                          select new { d.Number, d.Name, Company = c!.Name, Stage = s.Name, d.Amount, d.Currency, d.Probability, d.Status, d.ExpectedCloseDate, d.CreatedAt }).ToListAsync(ct);
        var bytes = Csv.Build(["Number", "Name", "Company", "Stage", "Amount", "Currency", "Probability", "Status", "ExpectedClose", "CreatedAt"],
            rows.Select(r => new object?[] { r.Number, r.Name, r.Company, r.Stage, r.Amount, r.Currency, r.Probability, r.Status.ToString(), r.ExpectedCloseDate, r.CreatedAt }));
        return File(bytes, "text/csv; charset=utf-8", $"deals-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
