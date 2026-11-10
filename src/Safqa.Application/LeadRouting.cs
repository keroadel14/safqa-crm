using Microsoft.EntityFrameworkCore;
using Safqa.Domain;
namespace Safqa.Application;

public class LeadRoutingService(IAppDbContext db)
{
    /// Round-robin over active reps (team-scoped when the rule has a team). Rule matching: exact source, then catch-all.
    public async Task<Guid?> PickOwnerAsync(Guid orgId, LeadSource source, CancellationToken ct)
    {
        var rule = await db.RoutingRules.Where(r => r.OrganizationId == orgId && r.Source == source).FirstOrDefaultAsync(ct)
                ?? await db.RoutingRules.Where(r => r.OrganizationId == orgId && r.Source == null).FirstOrDefaultAsync(ct);
        if (rule is null || rule.Strategy == RoutingStrategy.Manual) return null;

        var reps = await db.Profiles
            .Where(p => p.OrganizationId == orgId && p.IsActive && p.Role == UserRole.Rep
                        && (rule.TeamId == null || p.TeamId == rule.TeamId))
            .OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);
        if (reps.Count == 0) return null;

        var idx = rule.LastAssignedUserId is null ? -1 : reps.IndexOf(rule.LastAssignedUserId.Value);
        var next = reps[(idx + 1) % reps.Count];
        rule.LastAssignedUserId = next;   // persisted by caller's SaveChanges (same unit of work)
        return next;
    }
}
