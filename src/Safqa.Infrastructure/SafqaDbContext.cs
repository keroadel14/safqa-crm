using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
namespace Safqa.Infrastructure;

public class SafqaDbContext(DbContextOptions<SafqaDbContext> o) : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(o), IAppDbContext
{
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<PipelineStage> Stages => Set<PipelineStage>();
    public DbSet<DealStageHistory> StageHistory => Set<DealStageHistory>();
    public DbSet<LeadRoutingRule> RoutingRules => Set<LeadRoutingRule>();
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c) => c.Properties<decimal>().HavePrecision(18, 2);

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        // Global soft-delete filter for every BaseEntity
        foreach (var t in b.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
            typeof(SafqaDbContext).GetMethod(nameof(Filter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t.ClrType).Invoke(null, [b]);

        b.Entity<Deal>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.OwnerId, x.Status, x.ExpectedCloseDate });
            e.HasIndex(x => x.StageId);
            e.ToTable(t => { t.HasCheckConstraint("ck_deal_amount", "\"Amount\" >= 0"); t.HasCheckConstraint("ck_deal_prob", "\"Probability\" BETWEEN 0 AND 100"); });
        });
        b.Entity<Lead>(e => { e.HasIndex(x => new { x.OwnerId, x.Status }); e.HasIndex(x => x.Email); e.HasIndex(x => x.Phone); });
        b.Entity<Contact>(e => e.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique().HasFilter("\"Email\" IS NOT NULL AND \"DeletedAt\" IS NULL"));
        b.Entity<Company>(e => e.HasIndex(x => new { x.OrganizationId, x.Domain }).IsUnique().HasFilter("\"Domain\" IS NOT NULL AND \"DeletedAt\" IS NULL"));
        b.Entity<PipelineStage>(e => e.HasIndex(x => new { x.OrganizationId, x.Position }).IsUnique());
        b.Entity<DealStageHistory>(e => { e.Property(x => x.AmountAtChange).HasPrecision(18, 2); e.HasIndex(x => x.DealId); e.HasIndex(x => new { x.ToStageId, x.ChangedAt }); });
        b.Entity<AuditLog>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasSequence<long>("quote_seq").StartsAt(1);
        b.Entity<Quote>(e => { e.HasMany(q => q.Lines).WithOne().HasForeignKey(l => l.QuoteId); e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique(); e.HasIndex(x => x.DealId); });
        b.Entity<Product>().HasIndex(x => new { x.OrganizationId, x.Sku }).IsUnique();
        b.Entity<Team>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        b.Entity<TaskItem>().HasIndex(x => new { x.OwnerId, x.State, x.DueAt });
        b.Entity<Activity>().HasIndex(x => new { x.OwnerId, x.OccurredAt });
        b.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<ActivityLog>().HasIndex(x => new { x.EntityType, x.EntityId });
    }
    static void Filter<T>(ModelBuilder b) where T : BaseEntity => b.Entity<T>().HasQueryFilter(x => x.DeletedAt == null);

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var e in ChangeTracker.Entries<BaseEntity>().ToList())
        {
            if (e.State == EntityState.Deleted && e.Entity is not DealStageHistory) { e.State = EntityState.Modified; e.Entity.DeletedAt = now; }
            if (e.State is EntityState.Modified) e.Entity.UpdatedAt = now;
        }
        // Immutable audit trail for key entities
        var audits = ChangeTracker.Entries().Where(e => e.Entity is Deal or Lead or Contact or Company && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new AuditLog
            {
                EntityType = e.Entity.GetType().Name, EntityId = ((BaseEntity)e.Entity).Id, Action = e.State.ToString(), At = now,
                OldValues = e.State == EntityState.Added ? null : System.Text.Json.JsonSerializer.Serialize(e.Properties.Where(p => p.IsModified).ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
                NewValues = e.State == EntityState.Deleted ? null : System.Text.Json.JsonSerializer.Serialize(e.Properties.Where(p => e.State == EntityState.Added || p.IsModified).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))
            }).ToList();
        AuditLogs.AddRange(audits);
        return await base.SaveChangesAsync(ct);
    }
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime At { get; set; }
}
