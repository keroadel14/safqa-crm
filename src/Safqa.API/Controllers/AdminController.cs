using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
using Safqa.Infrastructure;
namespace Safqa.API.Controllers;

[ApiController, Route("api/v1/admin"), Authorize(Roles = "Admin")]
public class AdminController(SafqaDbContext db, ICurrentUser me, UserManager<IdentityUser<Guid>> um) : ControllerBase
{
    // ---------- Users ----------
    [HttpGet("users")]
    public async Task<List<UserAdminDto>> Users(CancellationToken ct) =>
        await (from p in db.Profiles where p.OrganizationId == me.OrganizationId join u in db.Users on p.Id equals u.Id orderby p.FullName
               select new UserAdminDto(p.Id, u.Email!, p.FullName, p.Role, p.TeamId, p.IsActive, p.DiscountLimitPct)).ToListAsync(ct);

    [HttpPost("users")]
    public async Task<UserAdminDto> CreateUser(CreateUserRequest r, CancellationToken ct)
    {
        var u = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = r.Email, Email = r.Email, EmailConfirmed = true };
        var res = await um.CreateAsync(u, r.Password);
        if (!res.Succeeded) throw new DomainRuleException("IDENTITY", string.Join("; ", res.Errors.Select(e => e.Description)));
        await um.AddToRoleAsync(u, RoleNames.For(r.Role));
        db.Profiles.Add(new UserProfile { Id = u.Id, OrganizationId = me.OrganizationId, FullName = r.FullName, Role = r.Role, TeamId = r.TeamId, DiscountLimitPct = r.DiscountLimitPct });
        await db.SaveChangesAsync(ct);
        return new UserAdminDto(u.Id, r.Email, r.FullName, r.Role, r.TeamId, true, r.DiscountLimitPct);
    }

    [HttpPut("users/{id:guid}")]
    public async Task<UserAdminDto> UpdateUser(Guid id, UpdateUserRequest r, CancellationToken ct)
    {
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == me.OrganizationId, ct) ?? throw new NotFoundException("User not found");
        if (id == me.UserId && (!r.IsActive || r.Role != UserRole.Admin)) throw new DomainRuleException("SELF_LOCKOUT", "You cannot demote or deactivate yourself");
        p.FullName = r.FullName; p.Role = r.Role; p.TeamId = r.TeamId; p.IsActive = r.IsActive; p.DiscountLimitPct = r.DiscountLimitPct;
        var u = (await um.FindByIdAsync(id.ToString()))!;
        await um.RemoveFromRolesAsync(u, await um.GetRolesAsync(u));
        await um.AddToRoleAsync(u, RoleNames.For(r.Role));
        if (!string.IsNullOrEmpty(r.NewPassword))
        {
            var res = await um.ResetPasswordAsync(u, await um.GeneratePasswordResetTokenAsync(u), r.NewPassword);
            if (!res.Succeeded) throw new DomainRuleException("IDENTITY", string.Join("; ", res.Errors.Select(e => e.Description)));
        }
        if (!r.IsActive) await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        return new UserAdminDto(id, u.Email!, p.FullName, p.Role, p.TeamId, p.IsActive, p.DiscountLimitPct);
    }

    // ---------- Teams ----------
    [HttpGet("teams")]
    public async Task<List<TeamDto>> Teams(CancellationToken ct) =>
        await db.Teams.Where(t => t.OrganizationId == me.OrganizationId).OrderBy(t => t.Name).Select(t => new TeamDto(t.Id, t.Name, t.ManagerId)).ToListAsync(ct);

    [HttpPost("teams")]
    public async Task<TeamDto> CreateTeam(TeamRequest r, CancellationToken ct)
    {
        if (await db.Teams.AnyAsync(t => t.OrganizationId == me.OrganizationId && t.Name == r.Name, ct)) throw new ConflictException("DUPLICATE_TEAM", "Team name already exists");
        var t = new Team { OrganizationId = me.OrganizationId, Name = r.Name, ManagerId = r.ManagerId };
        db.Teams.Add(t); await db.SaveChangesAsync(ct);
        return new TeamDto(t.Id, t.Name, t.ManagerId);
    }

    [HttpPut("teams/{id:guid}")]
    public async Task<TeamDto> UpdateTeam(Guid id, TeamRequest r, CancellationToken ct)
    {
        var t = await db.Teams.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == me.OrganizationId, ct) ?? throw new NotFoundException("Team not found");
        t.Name = r.Name; t.ManagerId = r.ManagerId; await db.SaveChangesAsync(ct);
        return new TeamDto(t.Id, t.Name, t.ManagerId);
    }

    [HttpDelete("teams/{id:guid}")]
    public async Task<IActionResult> DeleteTeam(Guid id, CancellationToken ct)
    {
        var t = await db.Teams.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == me.OrganizationId, ct) ?? throw new NotFoundException("Team not found");
        await db.Profiles.Where(p => p.TeamId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.TeamId, (Guid?)null), ct);
        db.Teams.Remove(t); await db.SaveChangesAsync(ct);     // soft delete via DbContext
        return NoContent();
    }

    // ---------- Pipeline stages ----------
    [HttpGet("stages")]
    public async Task<List<StageDto>> Stages(CancellationToken ct) =>
        await db.Stages.Where(s => s.OrganizationId == me.OrganizationId).OrderBy(s => s.Position)
            .Select(s => new StageDto(s.Id, s.Name, s.NameAr, s.Position, s.Kind, s.DefaultProbability)).ToListAsync(ct);

    [HttpPost("stages")]
    public async Task<StageDto> CreateStage(StageRequest r, CancellationToken ct)
    {
        var pos = (await db.Stages.Where(s => s.OrganizationId == me.OrganizationId).MaxAsync(s => (int?)s.Position, ct) ?? 0) + 1;
        var s = new PipelineStage { OrganizationId = me.OrganizationId, Name = r.Name, NameAr = r.NameAr, DefaultProbability = r.DefaultProbability, Kind = r.Kind, IsActive = r.IsActive, Position = pos };
        db.Stages.Add(s); await db.SaveChangesAsync(ct);
        return new StageDto(s.Id, s.Name, s.NameAr, s.Position, s.Kind, s.DefaultProbability);
    }

    [HttpPut("stages/{id:guid}")]
    public async Task<StageDto> UpdateStage(Guid id, StageRequest r, CancellationToken ct)
    {
        var s = await db.Stages.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == me.OrganizationId, ct) ?? throw new NotFoundException("Stage not found");
        if (!r.IsActive && await db.Deals.AnyAsync(d => d.StageId == id && d.Status == DealStatus.Open, ct))
            throw new ConflictException("STAGE_HAS_DEALS", "Cannot deactivate a stage that still has open deals");
        s.Name = r.Name; s.NameAr = r.NameAr; s.DefaultProbability = r.DefaultProbability; s.Kind = r.Kind; s.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        return new StageDto(s.Id, s.Name, s.NameAr, s.Position, s.Kind, s.DefaultProbability);
    }

    /// Two-phase renumbering inside one transaction so the unique (org, position) index is never violated.
    [HttpPut("stages/reorder")]
    public async Task<List<StageDto>> Reorder(ReorderRequest r, CancellationToken ct)
    {
        var stages = await db.Stages.Where(s => s.OrganizationId == me.OrganizationId).ToListAsync(ct);
        if (r.StageIds.Count != stages.Count || r.StageIds.Distinct().Count() != stages.Count || stages.Any(s => !r.StageIds.Contains(s.Id)))
            throw new DomainRuleException("INVALID_ORDER", "Order must list every stage exactly once");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var s in stages) s.Position += 1000;
        await db.SaveChangesAsync(ct);
        for (var i = 0; i < r.StageIds.Count; i++) stages.First(s => s.Id == r.StageIds[i]).Position = i + 1;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return stages.OrderBy(s => s.Position).Select(s => new StageDto(s.Id, s.Name, s.NameAr, s.Position, s.Kind, s.DefaultProbability)).ToList();
    }

    // ---------- Lead routing ----------
    [HttpGet("routing-rules")]
    public async Task<List<RoutingRuleDto>> Rules(CancellationToken ct) =>
        await db.RoutingRules.Where(r => r.OrganizationId == me.OrganizationId).Select(r => new RoutingRuleDto(r.Id, r.Source, r.TeamId, r.Strategy)).ToListAsync(ct);

    [HttpPost("routing-rules")]
    public async Task<RoutingRuleDto> CreateRule(RoutingRuleRequest r, CancellationToken ct)
    {
        if (await db.RoutingRules.AnyAsync(x => x.OrganizationId == me.OrganizationId && x.Source == r.Source, ct)) throw new ConflictException("DUPLICATE_RULE", "A rule for this source already exists");
        var rule = new LeadRoutingRule { OrganizationId = me.OrganizationId, Source = r.Source, TeamId = r.TeamId, Strategy = r.Strategy };
        db.RoutingRules.Add(rule); await db.SaveChangesAsync(ct);
        return new RoutingRuleDto(rule.Id, rule.Source, rule.TeamId, rule.Strategy);
    }

    [HttpPut("routing-rules/{id:guid}")]
    public async Task<RoutingRuleDto> UpdateRule(Guid id, RoutingRuleRequest r, CancellationToken ct)
    {
        var rule = await db.RoutingRules.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == me.OrganizationId, ct) ?? throw new NotFoundException("Rule not found");
        rule.Source = r.Source; rule.TeamId = r.TeamId; rule.Strategy = r.Strategy; await db.SaveChangesAsync(ct);
        return new RoutingRuleDto(rule.Id, rule.Source, rule.TeamId, rule.Strategy);
    }

    [HttpDelete("routing-rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var rule = await db.RoutingRules.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == me.OrganizationId, ct) ?? throw new NotFoundException("Rule not found");
        db.RoutingRules.Remove(rule); await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------- Audit ----------
    [HttpGet("audit-logs")]
    public async Task<PagedResult<AuditDto>> Audit([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? entityType = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AuditDto(a.Id, a.EntityType, a.EntityId, a.Action, a.OldValues, a.NewValues, a.At)).ToListAsync(ct);
        return new PagedResult<AuditDto>(items, total, page, pageSize);
    }
}
