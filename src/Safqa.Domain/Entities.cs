namespace Safqa.Domain;

public enum LeadStatus { New, Contacted, Qualified, Unqualified, Converted }
public enum LeadSource { Website, Referral, Exhibition, Social, ColdCall, Partner, Other }
public enum StageKind { Open, Won, Lost }
public enum DealStatus { Open, Won, Lost }
public enum ForecastCategory { Pipeline, BestCase, Commit, Omitted }
public enum RoutingStrategy { RoundRobin, Manual }
public enum UserRole { Admin, Manager, Rep }

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }          // soft delete
}

public abstract class OwnedEntity : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? OwnerId { get; set; }
}

public class Company : OwnedEntity
{
    public string Name { get; set; } = "";
    public string? Domain { get; set; }
    public string? Industry { get; set; }
    public string? City { get; set; }
    public string? VatNumber { get; set; }
}

public class Contact : OwnedEntity
{
    public Guid? CompanyId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }       // E.164
    public string? JobTitle { get; set; }
}

public class Lead : OwnedEntity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public string? UnqualifiedReason { get; set; }
    public string? Notes { get; set; }
    public string? UtmJson { get; set; }
    public DateTime? FirstContactedAt { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public Guid? ConvertedContactId { get; set; }
    public Guid? ConvertedCompanyId { get; set; }
    public Guid? ConvertedDealId { get; set; }
}

public class PipelineStage : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string NameAr { get; set; } = "";
    public int Position { get; set; }
    public int DefaultProbability { get; set; }
    public StageKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Deal : OwnedEntity
{
    public string Number { get; set; } = "";           // DL-####
    public string Name { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public Guid? PrimaryContactId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";
    public Guid StageId { get; set; }
    public int Probability { get; set; }
    public DealStatus Status { get; set; } = DealStatus.Open;
    public ForecastCategory ForecastCategory { get; set; } = ForecastCategory.Pipeline;
    public DateOnly ExpectedCloseDate { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? LostReason { get; set; }
    public string? Competitor { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? NextTaskAt { get; set; }
    public int Version { get; set; } = 1;              // optimistic locking token
}

public class DealStageHistory : BaseEntity
{
    public Guid DealId { get; set; }
    public Guid? FromStageId { get; set; }
    public Guid ToStageId { get; set; }
    public decimal AmountAtChange { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public class LeadRoutingRule : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public LeadSource? Source { get; set; }            // null = catch-all
    public Guid? TeamId { get; set; }
    public RoutingStrategy Strategy { get; set; } = RoutingStrategy.RoundRobin;
    public Guid? LastAssignedUserId { get; set; }
}

public class UserProfile : BaseEntity   // 1:1 with ASP.NET Identity user
{
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }
    public Guid? TeamId { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal DiscountLimitPct { get; set; }
}
