using Microsoft.EntityFrameworkCore;
using Safqa.Domain;
namespace Safqa.Application;

public interface IAppDbContext
{
    DbSet<Lead> Leads { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<Company> Companies { get; }
    DbSet<Deal> Deals { get; }
    DbSet<PipelineStage> Stages { get; }
    DbSet<DealStageHistory> StageHistory { get; }
    DbSet<LeadRoutingRule> RoutingRules { get; }
    DbSet<UserProfile> Profiles { get; }
    DbSet<ActivityLog> ActivityLogs { get; }
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ICurrentUser { Guid UserId { get; } Guid OrganizationId { get; } UserRole Role { get; } }

public class ConflictException(string code, string message) : Exception(message) { public string Code { get; } = code; }
public class NotFoundException(string message) : Exception(message) { }
public class DomainRuleException(string code, string message) : Exception(message) { public string Code { get; } = code; }
