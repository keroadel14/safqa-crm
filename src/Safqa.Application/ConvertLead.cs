using MediatR;
using Microsoft.EntityFrameworkCore;
using Safqa.Domain;
namespace Safqa.Application;

public enum MergeMode { New, Merge }
public record ContactChoice(MergeMode Mode, Guid? Id);
public record CompanyChoice(MergeMode Mode, Guid? Id, string? Name);
public record DealInput(string Name, decimal Amount, Guid StageId, DateOnly ExpectedClose);
public record ConvertLeadCommand(Guid LeadId, ContactChoice Contact, CompanyChoice Company, DealInput? Deal) : IRequest<ConvertLeadResult>;
public record ConvertLeadResult(Guid ContactId, Guid CompanyId, Guid? DealId);

public class ConvertLeadHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<ConvertLeadCommand, ConvertLeadResult>
{
    public async Task<ConvertLeadResult> Handle(ConvertLeadCommand c, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == c.LeadId && l.OrganizationId == user.OrganizationId, ct)
                   ?? throw new NotFoundException("Lead not found");
        if (lead.Status == LeadStatus.Converted) throw new ConflictException("LEAD_ALREADY_CONVERTED", "Lead already converted");
        if (lead.Status != LeadStatus.Qualified) throw new DomainRuleException("LEAD_NOT_QUALIFIED", "Only qualified leads can be converted");
        if (user.Role == UserRole.Rep && lead.OwnerId != user.UserId) throw new DomainRuleException("FORBIDDEN", "Not your lead");

        // ---- Company (dedupe by domain extracted from e-mail when creating new) ----
        Company company;
        if (c.Company.Mode == MergeMode.Merge)
            company = await db.Companies.FirstOrDefaultAsync(x => x.Id == c.Company.Id, ct) ?? throw new NotFoundException("Company not found");
        else
        {
            var domain = DomainOf(lead.Email);
            company = domain is null ? null! :
                await db.Companies.FirstOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.Domain == domain, ct);
            company ??= db.Companies.Add(new Company
            {
                OrganizationId = user.OrganizationId, OwnerId = lead.OwnerId ?? user.UserId,
                Name = c.Company.Name ?? lead.CompanyName ?? $"{lead.FirstName} {lead.LastName}", Domain = domain
            }).Entity;
        }

        // ---- Contact (dedupe by e-mail or phone, unless caller chose explicit merge) ----
        Contact? contact = null;
        if (c.Contact.Mode == MergeMode.Merge)
            contact = await db.Contacts.FirstOrDefaultAsync(x => x.Id == c.Contact.Id, ct) ?? throw new NotFoundException("Contact not found");
        else
        {
            var email = lead.Email?.ToLowerInvariant();
            contact = await db.Contacts.FirstOrDefaultAsync(x => x.OrganizationId == user.OrganizationId &&
                ((email != null && x.Email != null && x.Email.ToLower() == email) || (lead.Phone != null && x.Phone == lead.Phone)), ct);
            if (contact is not null)
                throw new ConflictException("DUPLICATE_CONTACT", $"Contact {contact.Id} already matches this e-mail/phone; choose merge.");
            contact = db.Contacts.Add(new Contact
            {
                OrganizationId = user.OrganizationId, OwnerId = lead.OwnerId ?? user.UserId, CompanyId = company.Id,
                FirstName = lead.FirstName, LastName = lead.LastName, Email = lead.Email, Phone = lead.Phone, JobTitle = lead.JobTitle
            }).Entity;
        }
        contact.CompanyId ??= company.Id;

        // ---- Deal ----
        Deal? deal = null;
        if (c.Deal is not null)
        {
            var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == c.Deal.StageId && s.IsActive, ct)
                        ?? throw new NotFoundException("Stage not found");
            var seq = await db.Deals.IgnoreQueryFilters().CountAsync(d => d.OrganizationId == user.OrganizationId, ct) + 1;
            deal = db.Deals.Add(new Deal
            {
                OrganizationId = user.OrganizationId, OwnerId = lead.OwnerId ?? user.UserId, Number = $"DL-{seq:0000}",
                Name = c.Deal.Name, Amount = c.Deal.Amount, StageId = stage.Id, Probability = stage.DefaultProbability,
                ExpectedCloseDate = c.Deal.ExpectedClose, CompanyId = company.Id, PrimaryContactId = contact.Id
            }).Entity;
            db.StageHistory.Add(new DealStageHistory { DealId = deal.Id, ToStageId = stage.Id, AmountAtChange = deal.Amount, ChangedBy = user.UserId });
        }

        lead.Status = LeadStatus.Converted; lead.ConvertedAt = DateTime.UtcNow;
        lead.ConvertedContactId = contact.Id; lead.ConvertedCompanyId = company.Id; lead.ConvertedDealId = deal?.Id;
        var leadName = $"{lead.FirstName} {lead.LastName}";
        db.ActivityLogs.Add(new ActivityLog { ActorId = user.UserId, EntityType = "Lead", EntityId = lead.Id, Action = "Converted", Description = $"Lead {leadName} converted" + (deal is null ? "" : $" into deal {deal.Number}") });
        if (deal is not null) db.ActivityLogs.Add(new ActivityLog { ActorId = user.UserId, EntityType = "Deal", EntityId = deal.Id, Action = "Created", Description = $"Deal created from lead {leadName}" });
        await db.SaveChangesAsync(ct);           // single transaction: all-or-nothing
        return new ConvertLeadResult(contact.Id, company.Id, deal?.Id);
    }

    static string? DomainOf(string? email)
    {
        var d = email?.Split('@').ElementAtOrDefault(1)?.ToLowerInvariant();
        return d is null or "gmail.com" or "outlook.com" or "hotmail.com" or "yahoo.com" ? null : d;
    }
}
