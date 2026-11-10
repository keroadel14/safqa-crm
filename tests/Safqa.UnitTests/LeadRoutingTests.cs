using FluentAssertions;
using Safqa.Application;
using Safqa.Domain;
using static Safqa.UnitTests.TestHelpers;
namespace Safqa.UnitTests;

public class LeadRoutingTests
{
    static async Task<(Infrastructure.SafqaDbContext db, LeadRoutingService svc)> Arrange(RoutingStrategy strategy, params (int n, bool active)[] reps)
    {
        var db = NewDb();
        foreach (var (n, active) in reps) db.Profiles.Add(new UserProfile { Id = Id(n), OrganizationId = Org, FullName = $"Rep {n}", Role = UserRole.Rep, IsActive = active });
        db.RoutingRules.Add(new LeadRoutingRule { OrganizationId = Org, Strategy = strategy });
        await db.SaveChangesAsync();
        return (db, new LeadRoutingService(db));
    }

    static async Task<List<Guid?>> Pick(Infrastructure.SafqaDbContext db, LeadRoutingService svc, int count)
    {
        var owners = new List<Guid?>();
        for (var i = 0; i < count; i++) { owners.Add(await svc.PickOwnerAsync(Org, LeadSource.Website, default)); await db.SaveChangesAsync(); }
        return owners;
    }

    [Fact]
    public async Task RoundRobin_distributes_consecutive_leads_evenly()
    {
        var (db, svc) = await Arrange(RoutingStrategy.RoundRobin, (1, true), (2, true), (3, true));
        var owners = await Pick(db, svc, 9);
        owners.Should().Equal(Id(1), Id(2), Id(3), Id(1), Id(2), Id(3), Id(1), Id(2), Id(3));
        owners.GroupBy(o => o).Should().OnlyContain(g => g.Count() == 3);
    }

    [Fact]
    public async Task Inactive_reps_are_excluded()
    {
        var (db, svc) = await Arrange(RoutingStrategy.RoundRobin, (1, true), (2, false), (3, true));
        var owners = await Pick(db, svc, 4);
        owners.Should().Equal(Id(1), Id(3), Id(1), Id(3));
        owners.Should().NotContain(Id(2));
    }

    [Fact]
    public async Task Managers_and_admins_are_not_routed_leads()
    {
        var (db, svc) = await Arrange(RoutingStrategy.RoundRobin, (1, true));
        db.Profiles.Add(new UserProfile { Id = Id(9), OrganizationId = Org, FullName = "Boss", Role = UserRole.Manager });
        await db.SaveChangesAsync();
        (await Pick(db, svc, 3)).Should().OnlyContain(o => o == Id(1));
    }

    [Fact]
    public async Task Manual_strategy_or_no_reps_returns_null()
    {
        var (db1, svc1) = await Arrange(RoutingStrategy.Manual, (1, true));
        (await svc1.PickOwnerAsync(Org, LeadSource.Website, default)).Should().BeNull();
        var (_, svc2) = await Arrange(RoutingStrategy.RoundRobin, (1, false));
        (await svc2.PickOwnerAsync(Org, LeadSource.Website, default)).Should().BeNull();
        db1.Dispose();
    }
}
