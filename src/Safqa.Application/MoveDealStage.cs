using MediatR;
using Microsoft.EntityFrameworkCore;
using Safqa.Domain;
namespace Safqa.Application;

public record WonInfo(decimal? Amount, DateTime? ClosedAt);
public record LostInfo(string Reason, string? Competitor);
public record MoveDealStageCommand(Guid DealId, Guid StageId, int Version, WonInfo? Won, LostInfo? Lost) : IRequest<Deal>;

public class MoveDealStageHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<MoveDealStageCommand, Deal>
{
    public async Task<Deal> Handle(MoveDealStageCommand c, CancellationToken ct)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == c.DealId && d.OrganizationId == user.OrganizationId, ct)
                   ?? throw new NotFoundException("Deal not found");
        if (user.Role == UserRole.Rep && deal.OwnerId != user.UserId) throw new DomainRuleException("FORBIDDEN", "Not your deal");

        // Optimistic lock: caller's version must equal what is stored; EF enforces it again at UPDATE time.
        if (deal.Version != c.Version) throw new ConflictException("VERSION_CONFLICT", "Deal was modified by another user");
        db.ChangeTracker.Entries<Deal>().First(e => e.Entity.Id == deal.Id).Property(x => x.Version).OriginalValue = c.Version;

        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == c.StageId && s.IsActive && s.OrganizationId == user.OrganizationId, ct)
                    ?? throw new NotFoundException("Stage not found");
        if (stage.Kind == StageKind.Lost && string.IsNullOrWhiteSpace(c.Lost?.Reason))
            throw new DomainRuleException("LOST_REASON_REQUIRED", "Lost reason is required");

        var from = deal.StageId;
        var fromName = await db.Stages.Where(s => s.Id == from).Select(s => s.Name).FirstOrDefaultAsync(ct);
        deal.StageId = stage.Id; deal.Probability = stage.DefaultProbability; deal.UpdatedAt = DateTime.UtcNow;
        switch (stage.Kind)
        {
            case StageKind.Won:
                deal.Status = DealStatus.Won; deal.Probability = 100; deal.ClosedAt = c.Won?.ClosedAt ?? DateTime.UtcNow;
                if (c.Won?.Amount is { } amt) deal.Amount = amt;
                break;
            case StageKind.Lost:
                deal.Status = DealStatus.Lost; deal.Probability = 0; deal.ClosedAt = DateTime.UtcNow;
                deal.LostReason = c.Lost!.Reason; deal.Competitor = c.Lost.Competitor;
                break;
            default:
                deal.Status = DealStatus.Open; deal.ClosedAt = null; deal.LostReason = null;
                break;
        }
        deal.Version++;
        db.ActivityLogs.Add(new ActivityLog
        {
            ActorId = user.UserId, EntityType = "Deal", EntityId = deal.Id, Action = stage.Kind == StageKind.Won ? "Won" : stage.Kind == StageKind.Lost ? "Lost" : "StageChanged",
            Description = $"Moved from {fromName ?? "-"} to {stage.Name}" + (stage.Kind == StageKind.Lost ? $" (reason: {c.Lost!.Reason})" : "")
        });
        db.StageHistory.Add(new DealStageHistory { DealId = deal.Id, FromStageId = from, ToStageId = stage.Id, AmountAtChange = deal.Amount, ChangedBy = user.UserId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("VERSION_CONFLICT", "Deal was modified by another user"); }
        return deal;
    }
}
