using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
using static Safqa.UnitTests.TestHelpers;
namespace Safqa.UnitTests;

public class ConvertLeadTests
{
    static readonly Guid Rep = Id(1);
    static ConvertLeadCommand Cmd(Guid leadId, Guid stageId) => new(leadId, new ContactChoice(MergeMode.New, null), new CompanyChoice(MergeMode.New, null, "Acme"),
        new DealInput("Acme deal", 5000, stageId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))));

    static async Task<(Infrastructure.SafqaDbContext db, Lead lead, PipelineStage[] stages)> Arrange(LeadStatus status, string email = "omar@acme.example")
    {
        var db = NewDb(); var stages = SeedStages(db);
        var lead = new Lead { OrganizationId = Org, OwnerId = Rep, FirstName = "Omar", LastName = "Farouk", Email = email, Phone = "+966500000001", CompanyName = "Acme", Status = status };
        db.Leads.Add(lead); await db.SaveChangesAsync();
        return (db, lead, stages);
    }

    [Fact]
    public async Task Qualified_lead_creates_company_contact_and_deal_together()
    {
        var (db, lead, stages) = await Arrange(LeadStatus.Qualified);
        var res = await new ConvertLeadHandler(db, User(Rep, UserRole.Rep)).Handle(Cmd(lead.Id, stages[0].Id), default);

        (await db.Companies.CountAsync()).Should().Be(1);
        (await db.Contacts.CountAsync()).Should().Be(1);
        var deal = await db.Deals.SingleAsync();
        deal.Probability.Should().Be(10); deal.Number.Should().Be("DL-0001"); deal.PrimaryContactId.Should().Be(res.ContactId);
        var saved = await db.Leads.SingleAsync();
        saved.Status.Should().Be(LeadStatus.Converted);
        saved.ConvertedContactId.Should().Be(res.ContactId); saved.ConvertedCompanyId.Should().Be(res.CompanyId); saved.ConvertedDealId.Should().Be(res.DealId);
        (await db.StageHistory.CountAsync()).Should().Be(1);
        (await db.ActivityLogs.CountAsync()).Should().Be(2);   // lead + deal timeline entries
    }

    [Theory]
    [InlineData(LeadStatus.New)]
    [InlineData(LeadStatus.Contacted)]
    [InlineData(LeadStatus.Unqualified)]
    public async Task Non_qualified_lead_is_rejected_and_nothing_is_created(LeadStatus status)
    {
        var (db, lead, stages) = await Arrange(status);
        Func<Task> act = () => new ConvertLeadHandler(db, User(Rep, UserRole.Rep)).Handle(Cmd(lead.Id, stages[0].Id), default);
        (await act.Should().ThrowAsync<DomainRuleException>()).Where(e => e.Code == "LEAD_NOT_QUALIFIED");
        (await db.Deals.CountAsync()).Should().Be(0); (await db.Contacts.CountAsync()).Should().Be(0); (await db.Companies.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Already_converted_lead_conflicts()
    {
        var (db, lead, stages) = await Arrange(LeadStatus.Converted);
        Func<Task> act = () => new ConvertLeadHandler(db, User(Rep, UserRole.Rep)).Handle(Cmd(lead.Id, stages[0].Id), default);
        (await act.Should().ThrowAsync<ConflictException>()).Where(e => e.Code == "LEAD_ALREADY_CONVERTED");
    }

    [Fact]
    public async Task Duplicate_contact_email_requires_explicit_merge()
    {
        var (db, lead, stages) = await Arrange(LeadStatus.Qualified);
        db.Contacts.Add(new Contact { OrganizationId = Org, OwnerId = Rep, FirstName = "Existing", LastName = "Person", Email = "OMAR@acme.example" });
        await db.SaveChangesAsync();
        Func<Task> act = () => new ConvertLeadHandler(db, User(Rep, UserRole.Rep)).Handle(Cmd(lead.Id, stages[0].Id), default);
        (await act.Should().ThrowAsync<ConflictException>()).Where(e => e.Code == "DUPLICATE_CONTACT");
        (await db.Leads.SingleAsync()).Status.Should().Be(LeadStatus.Qualified);
    }

    [Fact]
    public async Task Rep_cannot_convert_another_reps_lead()
    {
        var (db, lead, stages) = await Arrange(LeadStatus.Qualified);
        Func<Task> act = () => new ConvertLeadHandler(db, User(Id(2), UserRole.Rep)).Handle(Cmd(lead.Id, stages[0].Id), default);
        (await act.Should().ThrowAsync<DomainRuleException>()).Where(e => e.Code == "FORBIDDEN");
    }
}
