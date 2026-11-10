using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
using Safqa.Infrastructure;
namespace Safqa.API.Controllers;

[ApiController, Route("api/v1/leads"), Authorize]
public class LeadsController(SafqaDbContext db, ICurrentUser me, LeadRoutingService routing, IMediator mediator, IConfiguration cfg, IHubContext<NotificationsHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<LeadDto>> List([FromQuery] LeadQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page); var size = Math.Clamp(q.PageSize, 1, 100);
        var query = db.Leads.ScopedTo(me).AsQueryable();
        if (q.Status is { } st) query = query.Where(l => l.Status == st);
        if (q.OwnerId is { } oid) query = query.Where(l => l.OwnerId == oid);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var like = $"%{q.Q.Trim()}%";
            query = query.Where(l => EF.Functions.ILike(l.FirstName + " " + l.LastName, like) || (l.Email != null && EF.Functions.ILike(l.Email, like)) || (l.CompanyName != null && EF.Functions.ILike(l.CompanyName, like)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(l => new LeadDto(l.Id, l.FirstName, l.LastName, l.CompanyName, l.Email, l.Phone, l.Source, l.Status, l.OwnerId, l.CreatedAt)).ToListAsync(ct);
        return new PagedResult<LeadDto>(items, total, page, size);
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create(CreateLeadRequest r, CancellationToken ct)
    {
        Guid owner = me.Role == UserRole.Rep ? me.UserId : r.OwnerId ?? await routing.PickOwnerAsync(me.OrganizationId, r.Source, ct) ?? me.UserId;
        var l = new Lead
        {
            OrganizationId = me.OrganizationId, OwnerId = owner, FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(), CompanyName = r.CompanyName,
            JobTitle = r.JobTitle, Email = r.Email?.Trim().ToLowerInvariant(), Phone = r.Phone, Source = r.Source, Notes = r.Notes
        };
        db.Leads.Add(l);
        await db.SaveChangesAsync(ct);                  // also persists routing's LastAssignedUserId
        if (owner != me.UserId) await hub.Clients.User(owner.ToString()).SendAsync("leadAssigned", new { l.Id, name = $"{l.FirstName} {l.LastName}" }, ct);
        return Ok(new LeadDto(l.Id, l.FirstName, l.LastName, l.CompanyName, l.Email, l.Phone, l.Source, l.Status, l.OwnerId, l.CreatedAt));
    }

    /// Public web-form endpoint: origin allow-list, honeypot (`website`), IP rate limit.
    [HttpPost("public-capture"), AllowAnonymous, EnableRateLimiting("public-capture")]
    public async Task<IActionResult> Capture(PublicCaptureRequest r, CancellationToken ct)
    {
        var allowed = cfg.GetSection("PublicCapture:AllowedOrigins").Get<string[]>() ?? [];
        if (!allowed.Contains(Request.Headers.Origin.ToString(), StringComparer.OrdinalIgnoreCase))
            throw new DomainRuleException("FORBIDDEN", "Origin not allowed");
        if (!string.IsNullOrWhiteSpace(r.Website)) return Accepted(new { ok = true });   // bot: pretend success, store nothing

        var parts = r.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var org = Tenant.DefaultOrgId;
        var owner = await routing.PickOwnerAsync(org, LeadSource.Website, ct);
        static string Cut(string s, int n) => s.Length <= n ? s : s[..n];
        var l = new Lead
        {
            OrganizationId = org, OwnerId = owner, FirstName = parts[0], LastName = parts.Length > 1 ? parts[1] : "-", CompanyName = r.Company,
            Email = r.Email?.Trim().ToLowerInvariant(), Phone = r.Phone, Source = LeadSource.Website, Notes = r.Message,
            UtmJson = r.Utm is { Count: > 0 } ? JsonSerializer.Serialize(r.Utm.Take(10).ToDictionary(k => Cut(k.Key, 40), v => Cut(v.Value ?? "", 200))) : null
        };
        db.Leads.Add(l);
        await db.SaveChangesAsync(ct);
        if (owner is { } o) await hub.Clients.User(o.ToString()).SendAsync("leadAssigned", new { l.Id, name = $"{l.FirstName} {l.LastName}" }, ct);
        return Accepted(new { ok = true });
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<ConvertLeadResult> Convert(Guid id, ConvertLeadRequest r, CancellationToken ct)
        => await mediator.Send(new ConvertLeadCommand(id, r.Contact, r.Company, r.Deal), ct);

    [HttpPut("{id:guid}/status")]
    public async Task<LeadDto> SetStatus(Guid id, UpdateLeadStatusRequest r, CancellationToken ct)
    {
        var l = await db.Leads.ScopedTo(me).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Lead not found");
        if (l.Status == LeadStatus.Converted) throw new ConflictException("LEAD_ALREADY_CONVERTED", "Lead already converted");
        l.Status = r.Status;
        l.UnqualifiedReason = r.Status == LeadStatus.Unqualified ? r.UnqualifiedReason : null;
        if (r.Status is LeadStatus.Contacted or LeadStatus.Qualified) l.FirstContactedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new LeadDto(l.Id, l.FirstName, l.LastName, l.CompanyName, l.Email, l.Phone, l.Source, l.Status, l.OwnerId, l.CreatedAt);
    }

    /// CSV export (managers/admins only – reps cannot bulk-export).
    [HttpGet("export"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Export([FromQuery] LeadStatus? status, CancellationToken ct)
    {
        var q = db.Leads.ScopedTo(me).AsQueryable();
        if (status is { } s) q = q.Where(l => l.Status == s);
        var rows = await q.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        var bytes = Csv.Build(["FirstName", "LastName", "Company", "JobTitle", "Email", "Phone", "Source", "Status", "UnqualifiedReason", "OwnerId", "CreatedAt"],
            rows.Select(l => new object?[] { l.FirstName, l.LastName, l.CompanyName, l.JobTitle, l.Email, l.Phone, l.Source.ToString(), l.Status.ToString(), l.UnqualifiedReason, l.OwnerId, l.CreatedAt }));
        return File(bytes, "text/csv; charset=utf-8", $"leads-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
