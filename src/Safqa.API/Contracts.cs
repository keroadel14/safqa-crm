using FluentValidation;
using Safqa.Application;
using Safqa.Domain;
namespace Safqa.API;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record UserDto(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);
public record AuthResponse(string Token, string RefreshToken, UserDto User);
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public class LeadQuery { public int Page { get; set; } = 1; public int PageSize { get; set; } = 25; public LeadStatus? Status { get; set; } public Guid? OwnerId { get; set; } public string? Q { get; set; } }
public record LeadDto(Guid Id, string FirstName, string LastName, string? CompanyName, string? Email, string? Phone, LeadSource Source, LeadStatus Status, Guid? OwnerId, DateTime CreatedAt);
public record CreateLeadRequest(string FirstName, string LastName, string? CompanyName, string? JobTitle, string? Email, string? Phone, LeadSource Source, Guid? OwnerId, string? Notes);
public record PublicCaptureRequest(string Name, string? Email, string? Phone, string? Company, string? Message, Dictionary<string, string>? Utm, string? Website);
public record ConvertLeadRequest(ContactChoice Contact, CompanyChoice Company, DealInput? Deal);

public record StageDto(Guid Id, string Name, string NameAr, int Position, StageKind Kind, int DefaultProbability);
public record DealDto(Guid Id, string Number, string Name, decimal Amount, string Currency, Guid StageId, int Probability, int Version, string? CompanyName);
public record BoardDto(List<StageDto> Stages, List<DealDto> Deals);
public record MoveStageRequest(Guid StageId, int Version, WonInfo? Won, LostInfo? Lost);
public record MoveResult(Guid Id, int Version, Guid StageId, int Probability, DealStatus Status);

public record QuoteLineRequest(Guid? ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal DiscountPct, decimal VatRate);
public record CreateQuoteRequest(Guid DealId, DateOnly ValidUntil, string? Terms, List<QuoteLineRequest> Lines);
public record QuoteResult(Guid Id, string Number, QuoteStatus Status, decimal Total, string Currency, bool NeedsApproval);
public record QuoteListDto(Guid Id, string Number, string DealName, QuoteStatus Status, decimal Total, string Currency, DateTime CreatedAt);
public record ProductDto(Guid Id, string Name, string Sku, decimal UnitPrice, string Currency, decimal VatRate);
public record ProductRequest(string Name, string Sku, decimal UnitPrice, string Currency, decimal VatRate);

public record ForecastPoint(string Month, decimal Weighted, decimal Total);
public record DashboardDto(int OverdueTasks, int MeetingsToday, int UncontactedLeads, int UncontactedBreached, int StalledDeals, decimal WeightedForecast, decimal OpenPipeline, string Currency, List<ForecastPoint> Forecast);
public record SearchHit(string Type, Guid Id, string Title, string? Subtitle);

public record UserAdminDto(Guid Id, string Email, string FullName, UserRole Role, Guid? TeamId, bool IsActive, decimal DiscountLimitPct);
public record CreateUserRequest(string Email, string Password, string FullName, UserRole Role, Guid? TeamId, decimal DiscountLimitPct);
public record UpdateUserRequest(string FullName, UserRole Role, Guid? TeamId, bool IsActive, decimal DiscountLimitPct, string? NewPassword);
public record TeamDto(Guid Id, string Name, Guid? ManagerId);
public record TeamRequest(string Name, Guid? ManagerId);
public record StageRequest(string Name, string NameAr, int DefaultProbability, StageKind Kind, bool IsActive);
public record ReorderRequest(List<Guid> StageIds);
public record RoutingRuleDto(Guid Id, LeadSource? Source, Guid? TeamId, RoutingStrategy Strategy);
public record RoutingRuleRequest(LeadSource? Source, Guid? TeamId, RoutingStrategy Strategy);
public record AuditDto(long Id, string EntityType, Guid EntityId, string Action, string? OldValues, string? NewValues, DateTime At);

public class LoginValidator : AbstractValidator<LoginRequest> { public LoginValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress(); RuleFor(x => x.Password).NotEmpty(); } }

public class CreateLeadValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60); RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).Matches(@"^\+[1-9]\d{7,14}$").When(x => !string.IsNullOrEmpty(x.Phone)).WithMessage("Phone must be E.164");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone)).WithMessage("Email or phone is required");
        RuleFor(x => x.Notes).MaximumLength(5000);
    }
}

public class PublicCaptureValidator : AbstractValidator<PublicCaptureRequest>
{
    public PublicCaptureValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 80);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).Matches(@"^\+[1-9]\d{7,14}$").When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone)).WithMessage("Email or phone is required");
        RuleFor(x => x.Message).MaximumLength(2000); RuleFor(x => x.Company).MaximumLength(120);
    }
}

public class CreateQuoteValidator : AbstractValidator<CreateQuoteRequest>
{
    public CreateQuoteValidator()
    {
        RuleFor(x => x.DealId).NotEmpty();
        RuleFor(x => x.ValidUntil).Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Valid-until must not be in the past");
        RuleFor(x => x.Terms).MaximumLength(3000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
            l.RuleFor(x => x.Quantity).GreaterThan(0); l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.DiscountPct).InclusiveBetween(0, 100); l.RuleFor(x => x.VatRate).InclusiveBetween(0, 30);
        });
    }
}

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress(); RuleFor(x => x.Password).MinimumLength(8); RuleFor(x => x.FullName).NotEmpty().MaximumLength(100); RuleFor(x => x.DiscountLimitPct).InclusiveBetween(0, 100); }
}
public class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator() { RuleFor(x => x.FullName).NotEmpty().MaximumLength(100); RuleFor(x => x.DiscountLimitPct).InclusiveBetween(0, 100); RuleFor(x => x.NewPassword).MinimumLength(8).When(x => !string.IsNullOrEmpty(x.NewPassword)); }
}
public class StageRequestValidator : AbstractValidator<StageRequest>
{
    public StageRequestValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(60); RuleFor(x => x.NameAr).NotEmpty().MaximumLength(60); RuleFor(x => x.DefaultProbability).InclusiveBetween(0, 100); }
}
public class TeamRequestValidator : AbstractValidator<TeamRequest> { public TeamRequestValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(80); } }
public class ReorderValidator : AbstractValidator<ReorderRequest> { public ReorderValidator() { RuleFor(x => x.StageIds).NotEmpty(); } }
public class ProductValidator : AbstractValidator<ProductRequest>
{
    public ProductValidator() { RuleFor(x => x.Name).NotEmpty(); RuleFor(x => x.Sku).NotEmpty().MaximumLength(40); RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0); RuleFor(x => x.VatRate).InclusiveBetween(0, 30); RuleFor(x => x.Currency).Length(3); }
}

public record UpdateLeadStatusRequest(LeadStatus Status, string? UnqualifiedReason);
public record ActivityLogDto(long Id, string Action, string Description, string? ActorName, DateTime Timestamp);
public class UpdateLeadStatusValidator : AbstractValidator<UpdateLeadStatusRequest>
{
    public UpdateLeadStatusValidator()
    {
        RuleFor(x => x.Status).NotEqual(LeadStatus.Converted).WithMessage("Use the convert action");
        RuleFor(x => x.UnqualifiedReason).NotEmpty().MaximumLength(300).When(x => x.Status == LeadStatus.Unqualified).WithMessage("Reason is required");
    }
}
public record ResetPasswordRequest(string Email, string NewPassword);
