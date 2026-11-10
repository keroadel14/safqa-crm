namespace Safqa.Domain;

public static class Tenant { public static readonly Guid DefaultOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001"); }

public enum QuoteStatus { Draft, PendingApproval, Approved, Sent, Accepted, Rejected, Expired }
public enum ActivityType { Call, Meeting, Email, WhatsApp, Note }
public enum TaskState { Open, Done, Cancelled }

public class Team : BaseEntity { public Guid OrganizationId { get; set; } public string Name { get; set; } = ""; public Guid? ManagerId { get; set; } }

public class Product : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = ""; public string Sku { get; set; } = "";
    public decimal UnitPrice { get; set; } public string Currency { get; set; } = "SAR";
    public decimal VatRate { get; set; } = 15; public bool IsActive { get; set; } = true;
}

public class Quote : OwnedEntity
{
    public string Number { get; set; } = "";            // QT-####
    public Guid DealId { get; set; }
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public string Currency { get; set; } = "SAR";
    public decimal Subtotal { get; set; } public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; } public decimal Total { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string? Terms { get; set; }
    public Guid? ApprovedBy { get; set; } public DateTime? ApprovedAt { get; set; } public DateTime? SentAt { get; set; }
    public List<QuoteLine> Lines { get; set; } = [];
}

public class QuoteLine : BaseEntity
{
    public Guid QuoteId { get; set; } public Guid? ProductId { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; } public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; } public int Position { get; set; }
}

public class TaskItem : OwnedEntity
{
    public string Title { get; set; } = ""; public DateTime DueAt { get; set; }
    public TaskState State { get; set; } = TaskState.Open;
    public Guid? DealId { get; set; } public Guid? LeadId { get; set; }
}

public class Activity : OwnedEntity
{
    public ActivityType Type { get; set; } public string Subject { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid? DealId { get; set; } public Guid? LeadId { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid UserId { get; set; }
    public string TokenHash { get; set; } = ""; public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; } public string? ReplacedByHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ActivityLog   // immutable audit-style timeline entry shown on deal details
{
    public long Id { get; set; }
    public Guid ActorId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
