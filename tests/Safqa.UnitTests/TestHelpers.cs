using Microsoft.EntityFrameworkCore;
using Moq;
using Safqa.Application;
using Safqa.Domain;
using Safqa.Infrastructure;
namespace Safqa.UnitTests;

public static class TestHelpers
{
    public static readonly Guid Org = Tenant.DefaultOrgId;
    public static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:000000000000}");

    public static SafqaDbContext NewDb(string? name = null) =>
        new(new DbContextOptionsBuilder<SafqaDbContext>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options);

    public static ICurrentUser User(Guid id, UserRole role = UserRole.Admin)
    {
        var m = new Mock<ICurrentUser>();
        m.SetupGet(x => x.UserId).Returns(id); m.SetupGet(x => x.OrganizationId).Returns(Org); m.SetupGet(x => x.Role).Returns(role);
        return m.Object;
    }

    /// Initial 10 · Needs 25 · Proposal 50 · Negotiation 75 · Won 100 · Lost 0
    public static PipelineStage[] SeedStages(SafqaDbContext db)
    {
        (string n, int p, StageKind k)[] d = [("Initial", 10, StageKind.Open), ("Needs", 25, StageKind.Open), ("Proposal", 50, StageKind.Open), ("Negotiation", 75, StageKind.Open), ("Won", 100, StageKind.Won), ("Lost", 0, StageKind.Lost)];
        var stages = d.Select((x, i) => new PipelineStage { OrganizationId = Org, Name = x.n, NameAr = x.n, DefaultProbability = x.p, Kind = x.k, Position = i + 1 }).ToArray();
        db.Stages.AddRange(stages); db.SaveChanges();
        return stages;
    }
}
