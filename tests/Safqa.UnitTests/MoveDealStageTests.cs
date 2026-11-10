using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
using static Safqa.UnitTests.TestHelpers;
namespace Safqa.UnitTests;

public class MoveDealStageTests
{
    static readonly Guid Rep = Id(1);

    static (Infrastructure.SafqaDbContext db, Deal deal, PipelineStage[] st) Arrange(string? dbName = null)
    {
        var db = NewDb(dbName); var st = SeedStages(db);
        var deal = new Deal { OrganizationId = Org, OwnerId = Rep, Number = "DL-0001", Name = "Test", Amount = 1000, StageId = st[0].Id, Probability = 10, ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) };
        db.Deals.Add(deal); db.SaveChanges();
        return (db, deal, st);
    }

    [Fact]
    public async Task Move_updates_stage_probability_version_history_and_log()
    {
        var (db, deal, st) = Arrange();
        var res = await new MoveDealStageHandler(db, User(Rep, UserRole.Rep)).Handle(new MoveDealStageCommand(deal.Id, st[2].Id, 1, null, null), default);
        res.StageId.Should().Be(st[2].Id); res.Probability.Should().Be(50); res.Version.Should().Be(2); res.Status.Should().Be(DealStatus.Open);
        var h = await db.StageHistory.SingleAsync(); h.FromStageId.Should().Be(st[0].Id); h.ToStageId.Should().Be(st[2].Id);
        (await db.ActivityLogs.SingleAsync()).Description.Should().Contain("Initial").And.Contain("Proposal");
    }

    [Fact]
    public async Task Outdated_version_returns_conflict_and_changes_nothing()
    {
        var (db, deal, st) = Arrange();
        Func<Task> act = () => new MoveDealStageHandler(db, User(Rep, UserRole.Rep)).Handle(new MoveDealStageCommand(deal.Id, st[1].Id, 7, null, null), default);
        (await act.Should().ThrowAsync<ConflictException>()).Where(e => e.Code == "VERSION_CONFLICT");   // maps to HTTP 409
        (await db.Deals.SingleAsync()).StageId.Should().Be(st[0].Id);
        (await db.StageHistory.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Concurrent_edit_detected_by_the_database_token_is_translated_to_conflict()
    {
        var name = Guid.NewGuid().ToString();
        var (a, deal, st) = Arrange(name);                 // context A tracks the deal at version 1
        using var b = NewDb(name);
        await new MoveDealStageHandler(b, User(Rep, UserRole.Rep)).Handle(new MoveDealStageCommand(deal.Id, st[1].Id, 1, null, null), default);   // B wins → version 2
        Func<Task> act = () => new MoveDealStageHandler(a, User(Rep, UserRole.Rep)).Handle(new MoveDealStageCommand(deal.Id, st[2].Id, 1, null, null), default);
        (await act.Should().ThrowAsync<ConflictException>()).Where(e => e.Code == "VERSION_CONFLICT");
        a.Dispose();
    }

    [Fact]
    public async Task Won_sets_status_probability_and_close_date()
    {
        var (db, deal, st) = Arrange();
        var res = await new MoveDealStageHandler(db, User(Rep, UserRole.Rep)).Handle(new MoveDealStageCommand(deal.Id, st[4].Id, 1, new WonInfo(1200, null), null), default);
        res.Status.Should().Be(DealStatus.Won); res.Probability.Should().Be(100); res.Amount.Should().Be(1200); res.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Lost_requires_a_reason()
    {
        var (db, deal, st) = Arrange();
        var h = new MoveDealStageHandler(db, User(Rep, UserRole.Rep));
        Func<Task> bad = () => h.Handle(new MoveDealStageCommand(deal.Id, st[5].Id, 1, null, null), default);
        (await bad.Should().ThrowAsync<DomainRuleException>()).Where(e => e.Code == "LOST_REASON_REQUIRED");
        var ok = await h.Handle(new MoveDealStageCommand(deal.Id, st[5].Id, 1, null, new LostInfo("Price", "Competitor X")), default);
        ok.Status.Should().Be(DealStatus.Lost); ok.Probability.Should().Be(0); ok.LostReason.Should().Be("Price");
    }

    [Fact]
    public async Task Rep_cannot_move_someone_elses_deal()
    {
        var (db, deal, st) = Arrange();
        Func<Task> act = () => new MoveDealStageHandler(db, User(Id(2), UserRole.Rep)).Handle(new MoveDealStageCommand(deal.Id, st[1].Id, 1, null, null), default);
        (await act.Should().ThrowAsync<DomainRuleException>()).Where(e => e.Code == "FORBIDDEN");
    }
}
