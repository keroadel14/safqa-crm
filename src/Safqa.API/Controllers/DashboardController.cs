using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
using Safqa.Infrastructure;
namespace Safqa.API.Controllers;

[ApiController, Authorize, Route("api/v1")]
public class DashboardController(SafqaDbContext db, ICurrentUser me, IConfiguration cfg) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<DashboardDto> Get(CancellationToken ct)
    {
        var now = DateTime.UtcNow; var today = now.Date; var fourH = now.AddHours(-4);
        var overdue = await db.Tasks.ScopedTo(me).CountAsync(t => t.State == TaskState.Open && t.DueAt < now, ct);
        var meetings = await db.Activities.ScopedTo(me).CountAsync(a => a.Type == ActivityType.Meeting && a.OccurredAt >= today && a.OccurredAt < today.AddDays(1), ct);
        var fresh = db.Leads.ScopedTo(me).Where(l => l.Status == LeadStatus.New && l.FirstContactedAt == null);
        var uncontacted = await fresh.CountAsync(ct);
        var breached = await fresh.CountAsync(l => l.CreatedAt < fourH, ct);
        var open = db.Deals.ScopedTo(me).Where(d => d.Status == DealStatus.Open);
        var stalled = await open.CountAsync(d => d.NextTaskAt == null || d.NextTaskAt < now, ct);

        var from = new DateOnly(now.Year, now.Month, 1); var to = from.AddMonths(6);
        var rows = await open.Where(d => d.ExpectedCloseDate >= from && d.ExpectedCloseDate < to)
            .Select(d => new { d.ExpectedCloseDate, d.Amount, d.Probability }).ToListAsync(ct);
        var forecast = Enumerable.Range(0, 6).Select(i => from.AddMonths(i)).Select(m =>
        {
            var r = rows.Where(x => x.ExpectedCloseDate.Year == m.Year && x.ExpectedCloseDate.Month == m.Month).ToList();
            return new ForecastPoint($"{m.Year}-{m.Month:00}", r.Sum(x => x.Amount * x.Probability / 100m), r.Sum(x => x.Amount));
        }).ToList();
        var all = await open.Select(d => new { d.Amount, d.Probability }).ToListAsync(ct);
        return new DashboardDto(overdue, meetings, uncontacted, breached, stalled, all.Sum(x => x.Amount * x.Probability / 100m), all.Sum(x => x.Amount), cfg["Org:Currency"] ?? "SAR", forecast);
    }

    [HttpGet("search")]
    public async Task<List<SearchHit>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return [];
        var like = $"%{q.Trim()}%";
        var leads = await db.Leads.ScopedTo(me).Where(l => EF.Functions.ILike(l.FirstName + " " + l.LastName, like) || (l.Email != null && EF.Functions.ILike(l.Email, like)))
            .Take(5).Select(l => new SearchHit("lead", l.Id, l.FirstName + " " + l.LastName, l.Email)).ToListAsync(ct);
        var deals = await db.Deals.ScopedTo(me).Where(d => EF.Functions.ILike(d.Name, like) || EF.Functions.ILike(d.Number, like))
            .Take(5).Select(d => new SearchHit("deal", d.Id, d.Name, d.Number)).ToListAsync(ct);
        var quotes = await db.Quotes.ScopedTo(me).Where(x => EF.Functions.ILike(x.Number, like))
            .Take(5).Select(x => new SearchHit("quote", x.Id, x.Number, null)).ToListAsync(ct);
        return [.. leads, .. deals, .. quotes];
    }
}
