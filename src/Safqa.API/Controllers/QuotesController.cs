using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Safqa.Application;
using Safqa.Domain;
using Safqa.Infrastructure;
namespace Safqa.API.Controllers;

[ApiController, Route("api/v1/quotes"), Authorize]
public class QuotesController(SafqaDbContext db, ICurrentUser me, QuotePdfService pdf, IConfiguration cfg) : ControllerBase
{
    [HttpGet]
    public async Task<List<QuoteListDto>> List(CancellationToken ct) =>
        await (from q in db.Quotes.ScopedTo(me) join d in db.Deals on q.DealId equals d.Id orderby q.CreatedAt descending
               select new QuoteListDto(q.Id, q.Number, d.Name, q.Status, q.Total, q.Currency, q.CreatedAt)).Take(50).ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<QuoteResult>> Create(CreateQuoteRequest r, CancellationToken ct)
    {
        var deal = await db.Deals.ScopedTo(me).FirstOrDefaultAsync(d => d.Id == r.DealId, ct) ?? throw new NotFoundException("Deal not found");
        var calc = QuoteCalculator.Calculate(r.Lines.Select(l => new QuoteCalculator.Line(l.Quantity, l.UnitPrice, l.DiscountPct, l.VatRate)));

        // Discount approval workflow: any line above the user's limit puts the quote in PendingApproval.
        decimal limit = me.Role == UserRole.Admin ? 100 : (await db.Profiles.FirstAsync(p => p.Id == me.UserId, ct)).DiscountLimitPct;
        var needs = r.Lines.Max(l => l.DiscountPct) > limit;

        var seq = await db.Database.SqlQuery<long>($"SELECT nextval('quote_seq') AS \"Value\"").SingleAsync(ct);
        var q = new Quote
        {
            OrganizationId = me.OrganizationId, OwnerId = me.UserId, Number = $"QT-{seq:0000}", DealId = deal.Id, Currency = deal.Currency,
            Status = needs ? QuoteStatus.PendingApproval : QuoteStatus.Draft, ValidUntil = r.ValidUntil, Terms = r.Terms,
            Subtotal = calc.Subtotal, DiscountTotal = calc.DiscountTotal, VatTotal = calc.VatTotal, Total = calc.Total,
            Lines = r.Lines.Select((l, i) => new QuoteLine
            {
                ProductId = l.ProductId, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                DiscountPct = l.DiscountPct, VatRate = l.VatRate, LineTotal = calc.LineTotals[i], Position = i
            }).ToList()
        };
        db.Quotes.Add(q);
        await db.SaveChangesAsync(ct);
        return new QuoteResult(q.Id, q.Number, q.Status, q.Total, q.Currency, needs);
    }

    [HttpPost("{id:guid}/approve"), Authorize(Roles = "Admin,Manager")]
    public async Task<QuoteResult> Approve(Guid id, CancellationToken ct)
    {
        var q = await db.Quotes.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == me.OrganizationId, ct) ?? throw new NotFoundException("Quote not found");
        if (q.Status != QuoteStatus.PendingApproval) throw new ConflictException("INVALID_STATE", "Quote is not pending approval");
        if (q.OwnerId == me.UserId && me.Role != UserRole.Admin) throw new DomainRuleException("FORBIDDEN", "You cannot approve your own quote");
        q.Status = QuoteStatus.Approved; q.ApprovedBy = me.UserId; q.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new QuoteResult(q.Id, q.Number, q.Status, q.Total, q.Currency, false);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] string lang = "en", CancellationToken ct = default)
    {
        var q = await db.Quotes.ScopedTo(me).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Quote not found");
        var deal = await db.Deals.FirstAsync(d => d.Id == q.DealId, ct);
        var company = deal.CompanyId is { } cid ? await db.Companies.Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
        var bytes = pdf.Generate(q, deal.Name, company, cfg["Org:Name"] ?? "Safqa", lang);
        return File(bytes, "application/pdf", $"{q.Number}.pdf");
    }
}

[ApiController, Route("api/v1/products"), Authorize]
public class ProductsController(SafqaDbContext db, ICurrentUser me) : ControllerBase
{
    [HttpGet]
    public async Task<List<ProductDto>> List(CancellationToken ct) =>
        await db.Products.Where(p => p.OrganizationId == me.OrganizationId && p.IsActive).OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.Sku, p.UnitPrice, p.Currency, p.VatRate)).ToListAsync(ct);

    [HttpPost, Authorize(Roles = "Admin,Manager")]
    public async Task<ProductDto> Create(ProductRequest r, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(p => p.OrganizationId == me.OrganizationId && p.Sku == r.Sku, ct)) throw new ConflictException("DUPLICATE_SKU", "SKU already exists");
        var p = new Product { OrganizationId = me.OrganizationId, Name = r.Name, Sku = r.Sku, UnitPrice = r.UnitPrice, Currency = r.Currency.ToUpperInvariant(), VatRate = r.VatRate };
        db.Products.Add(p); await db.SaveChangesAsync(ct);
        return new ProductDto(p.Id, p.Name, p.Sku, p.UnitPrice, p.Currency, p.VatRate);
    }
}
