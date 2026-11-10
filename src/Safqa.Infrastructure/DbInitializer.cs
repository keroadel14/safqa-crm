using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Safqa.Application;
using Safqa.Domain;
namespace Safqa.Infrastructure;

public static class RoleNames
{
    public const string Admin = "Admin", SalesManager = "SalesManager", SalesRep = "SalesRep";
    public static readonly string[] All = [Admin, SalesManager, SalesRep];
    public static string For(UserRole r) => r switch { UserRole.Admin => Admin, UserRole.Manager => SalesManager, _ => SalesRep };
}

public static class DbInitializer
{
    public const string DemoAdmin = "admin@safqa.com", DemoManager = "manager@safqa.com", DemoAhmed = "ahmed@safqa.com", DemoSarah = "sarah@safqa.com";

    public static async Task SeedAsync(IServiceProvider sp, bool demo, string? adminPassword = null, CancellationToken ct = default)
    {
        var db = sp.GetRequiredService<SafqaDbContext>();
        var um = sp.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var rm = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct); else await db.Database.EnsureCreatedAsync(ct);

        foreach (var r in RoleNames.All) if (!await rm.RoleExistsAsync(r)) await rm.CreateAsync(new IdentityRole<Guid>(r));

        var org = Tenant.DefaultOrgId;
        if (!await db.Stages.AnyAsync(ct))
        {
            (string en, string ar, int p, StageKind k)[] s =
            [("Initial Contact", "تواصل أولي", 10, StageKind.Open), ("Needs Identified", "تحديد الاحتياج", 25, StageKind.Open),
             ("Proposal / Quote", "عرض سعر", 50, StageKind.Open), ("Negotiation", "تفاوض", 75, StageKind.Open),
             ("Won", "رابحة", 100, StageKind.Won), ("Lost", "خاسرة", 0, StageKind.Lost)];
            for (var i = 0; i < s.Length; i++)
                db.Stages.Add(new PipelineStage { OrganizationId = org, Name = s[i].en, NameAr = s[i].ar, DefaultProbability = s[i].p, Kind = s[i].k, Position = i + 1 });
            db.RoutingRules.Add(new LeadRoutingRule { OrganizationId = org, Strategy = RoutingStrategy.RoundRobin });
            await db.SaveChangesAsync(ct);
        }

        if (!demo)
        {
            if (await db.Profiles.AnyAsync(ct)) return;
            if (string.IsNullOrWhiteSpace(adminPassword)) throw new InvalidOperationException("No users exist: set SEED_ADMIN_PASSWORD (or Seed__Demo=true for demo data).");
            await EnsureUser(um, db, DemoAdmin, adminPassword, "Administrator", UserRole.Admin, null, 100);
            await db.SaveChangesAsync(ct);
            return;
        }
        await SeedDemoAsync(db, um, ct);
    }

    static async Task<Guid> EnsureUser(UserManager<IdentityUser<Guid>> um, SafqaDbContext db, string email, string password, string name, UserRole role, Guid? teamId, decimal discountLimit)
    {
        var u = await um.FindByEmailAsync(email);
        if (u is null)
        {
            u = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
            var res = await um.CreateAsync(u, password);
            if (!res.Succeeded) throw new InvalidOperationException($"Seeding {email} failed: " + string.Join("; ", res.Errors.Select(e => e.Description)));
            await um.AddToRoleAsync(u, RoleNames.For(role));
        }
        if (!await db.Profiles.AnyAsync(p => p.Id == u.Id))
            db.Profiles.Add(new UserProfile { Id = u.Id, OrganizationId = Tenant.DefaultOrgId, FullName = name, Role = role, TeamId = teamId, DiscountLimitPct = discountLimit });
        return u.Id;
    }

    static async Task SeedDemoAsync(SafqaDbContext db, UserManager<IdentityUser<Guid>> um, CancellationToken ct)
    {
        var org = Tenant.DefaultOrgId; var now = DateTime.UtcNow;
        var teamId = Guid.NewGuid();
        await EnsureUser(um, db, DemoAdmin, "Safqa@Admin2026!", "Admin User", UserRole.Admin, null, 100);
        var manager = await EnsureUser(um, db, DemoManager, "Safqa@Manager2026!", "Mariam Al-Saud", UserRole.Manager, teamId, 20);
        var ahmed = await EnsureUser(um, db, DemoAhmed, "Safqa@Rep2026!", "Ahmed Khalil", UserRole.Rep, teamId, 10);
        var sarah = await EnsureUser(um, db, DemoSarah, "Safqa@Rep2026!", "Sarah Mansour", UserRole.Rep, teamId, 10);
        await db.SaveChangesAsync(ct);
        if (await db.Leads.AnyAsync(ct) || await db.Deals.AnyAsync(ct)) return;      // demo data only once

        db.Teams.Add(new Team { Id = teamId, OrganizationId = org, Name = "Sales Team A", ManagerId = manager });
        var st = await db.Stages.Where(s => s.OrganizationId == org).OrderBy(s => s.Position).ToListAsync(ct);   // [0..5]

        // Products
        var prods = new[] { ("ERP Licence (annual)", "ERP-LIC", 120000m), ("Implementation day", "IMP-DAY", 1500m), ("GPS tracker unit", "TRK-100", 1100m), ("Cloud subscription (month)", "SUB-CLD", 800m) }
            .Select(p => new Product { OrganizationId = org, Name = p.Item1, Sku = p.Item2, UnitPrice = p.Item3, VatRate = 15 }).ToList();
        db.Products.AddRange(prods);

        // Companies & contacts
        Company Co(string n, string d, string city, Guid owner) => new() { OrganizationId = org, OwnerId = owner, Name = n, Domain = d, City = city, Industry = "B2B" };
        var c1 = Co("Al Noor Trading", "alnoor.sa", "Riyadh", ahmed); var c2 = Co("Gulf Medical Supplies", "gulfmed.com", "Jeddah", sarah); var c3 = Co("Nile Logistics", "nile-logistics.com", "Dammam", ahmed);
        db.Companies.AddRange(c1, c2, c3);
        Contact Ct(Company c, string f, string l, string e, string ph, string title) => new() { OrganizationId = org, OwnerId = c.OwnerId, CompanyId = c.Id, FirstName = f, LastName = l, Email = e, Phone = ph, JobTitle = title };
        var k1 = Ct(c1, "Faisal", "Al-Noor", "faisal@alnoor.sa", "+966501110001", "CEO");
        var k2 = Ct(c2, "Huda", "Barakat", "huda@gulfmed.com", "+966501110002", "Procurement Lead");
        var k3 = Ct(c3, "Tariq", "Nasser", "tariq@nile-logistics.com", "+966501110003", "Operations Director");
        db.Contacts.AddRange(k1, k2, k3);

        // Deals (one per stage family) – StageHistory + ActivityLog entries
        int n = 0;
        Deal Dl(string name, decimal amt, int stage, Guid owner, Company c, Contact k, int closeInDays, DateTime? nextTask, ForecastCategory fc)
        {
            var s = st[stage];
            var d = new Deal
            {
                OrganizationId = org, OwnerId = owner, Number = $"DL-{++n:0000}", Name = name, Amount = amt, CompanyId = c.Id, PrimaryContactId = k.Id,
                StageId = s.Id, Probability = s.DefaultProbability, ExpectedCloseDate = DateOnly.FromDateTime(now.AddDays(closeInDays)), NextTaskAt = nextTask,
                ForecastCategory = fc, Status = s.Kind == StageKind.Won ? DealStatus.Won : DealStatus.Open, ClosedAt = s.Kind == StageKind.Won ? now.AddDays(-3) : null,
                LastActivityAt = now.AddDays(-1)
            };
            db.StageHistory.Add(new DealStageHistory { DealId = d.Id, ToStageId = s.Id, AmountAtChange = amt, ChangedBy = owner, ChangedAt = now.AddDays(-10) });
            db.ActivityLogs.Add(new ActivityLog { ActorId = owner, EntityType = "Deal", EntityId = d.Id, Action = "Created", Description = "Deal created", Timestamp = now.AddDays(-10) });
            if (stage > 0) db.ActivityLogs.Add(new ActivityLog { ActorId = owner, EntityType = "Deal", EntityId = d.Id, Action = s.Kind == StageKind.Won ? "Won" : "StageChanged", Description = $"Moved from {st[0].Name} to {s.Name}", Timestamp = now.AddDays(-3) });
            db.Deals.Add(d); return d;
        }
        var d1 = Dl("Al Noor – ERP rollout", 180000, 2, ahmed, c1, k1, 30, now.AddDays(2), ForecastCategory.BestCase);
        var d2 = Dl("Gulf Medical – annual supply", 95000, 1, sarah, c2, k2, 45, null, ForecastCategory.Pipeline);        // stalled: no next action
        var d3 = Dl("Nile Logistics – fleet tracking", 240000, 3, ahmed, c3, k3, 14, now.AddDays(1), ForecastCategory.Commit);
        var d4 = Dl("Al Noor – support renewal", 48000, 4, sarah, c1, k1, -3, null, ForecastCategory.Commit);

        // Leads
        Lead Ld(string f, string l, string comp, string email, string phone, LeadSource src, LeadStatus status, Guid owner, int ageHours, bool contacted = false) => new()
        {
            OrganizationId = org, OwnerId = owner, FirstName = f, LastName = l, CompanyName = comp, Email = email, Phone = phone, Source = src, Status = status,
            CreatedAt = now.AddHours(-ageHours), FirstContactedAt = contacted ? now.AddHours(-ageHours + 1) : null
        };
        db.Leads.AddRange(
            Ld("Khalid", "Al-Otaibi", "Riyadh Tech Solutions", "khalid@rts.example", "+966502220001", LeadSource.Website, LeadStatus.New, ahmed, 1),
            Ld("Mona", "Hassan", "Hassan Pharma", "mona@hassanpharma.example", "+966502220002", LeadSource.Referral, LeadStatus.Contacted, sarah, 30, true),
            Ld("Omar", "Farouk", "Farouk Contracting", "omar@farouk.example", "+966502220003", LeadSource.Exhibition, LeadStatus.Qualified, ahmed, 72, true),
            Ld("Layla", "Mansour", "Mansour Foods", "layla@mansourfoods.example", "+966502220004", LeadSource.ColdCall, LeadStatus.New, sarah, 6),
            new Lead
            {
                OrganizationId = org, OwnerId = ahmed, FirstName = "Faisal", LastName = "Al-Noor", CompanyName = c1.Name, Email = k1.Email, Phone = k1.Phone, Source = LeadSource.Partner, Status = LeadStatus.Converted,
                CreatedAt = now.AddDays(-14), FirstContactedAt = now.AddDays(-14).AddHours(1), ConvertedAt = now.AddDays(-12), ConvertedContactId = k1.Id, ConvertedCompanyId = c1.Id, ConvertedDealId = d1.Id
            });

        // Quotes (numbers come from the same DB sequence the API uses)
        async Task<Quote> MakeQuote(Deal d, Guid owner, decimal ownerLimit, QuoteStatus status, string terms, params (string desc, Guid? prod, decimal qty, decimal price, decimal disc)[] ls)
        {
            var calc = QuoteCalculator.Calculate(ls.Select(l => new QuoteCalculator.Line(l.qty, l.price, l.disc, 15)));
            var seq = await db.Database.SqlQuery<long>($"SELECT nextval('quote_seq') AS \"Value\"").SingleAsync(ct);
            var q = new Quote
            {
                OrganizationId = org, OwnerId = owner, Number = $"QT-{seq:0000}", DealId = d.Id, Currency = d.Currency, Terms = terms,
                Status = ls.Max(l => l.disc) > ownerLimit ? QuoteStatus.PendingApproval : status, ValidUntil = DateOnly.FromDateTime(now.AddDays(14)),
                Subtotal = calc.Subtotal, DiscountTotal = calc.DiscountTotal, VatTotal = calc.VatTotal, Total = calc.Total,
                Lines = ls.Select((l, i) => new QuoteLine { ProductId = l.prod, Description = l.desc, Quantity = l.qty, UnitPrice = l.price, DiscountPct = l.disc, VatRate = 15, LineTotal = calc.LineTotals[i], Position = i }).ToList()
            };
            db.Quotes.Add(q); return q;
        }
        const string defaultTerms = "Payment: 30 days net. Prices in SAR, VAT 15% included in totals. Valid for 14 days.";
        await MakeQuote(d1, ahmed, 10, QuoteStatus.Sent, defaultTerms, ("ERP licence (annual)", prods[0].Id, 1, 120000, 0), ("Implementation services (days)", prods[1].Id, 40, 1500, 5));
        await MakeQuote(d3, ahmed, 10, QuoteStatus.Draft, defaultTerms, ("GPS tracker unit", prods[2].Id, 120, 1100, 12), ("Cloud subscription (months)", prods[3].Id, 12, 800, 0));   // 12% > rep limit → pending approval

        // Tasks & activities so the daily dashboard is populated
        db.Tasks.AddRange(
            new TaskItem { OrganizationId = org, OwnerId = ahmed, Title = "Send revised ERP proposal", DueAt = now.AddDays(-1), DealId = d1.Id },
            new TaskItem { OrganizationId = org, OwnerId = ahmed, Title = "Prepare fleet-tracking demo", DueAt = now.AddDays(1), DealId = d3.Id },
            new TaskItem { OrganizationId = org, OwnerId = sarah, Title = "Call Gulf Medical procurement", DueAt = now.AddDays(-2), DealId = d2.Id });
        db.Activities.AddRange(
            new Activity { OrganizationId = org, OwnerId = ahmed, Type = ActivityType.Meeting, Subject = "Pricing review with Nile Logistics", OccurredAt = now.Date.AddHours(10), DealId = d3.Id },
            new Activity { OrganizationId = org, OwnerId = sarah, Type = ActivityType.Call, Subject = "Intro call with Gulf Medical", OccurredAt = now.AddDays(-1), DealId = d2.Id });
        await db.SaveChangesAsync(ct);
    }
}
