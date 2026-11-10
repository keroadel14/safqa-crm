using FluentValidation;
using MediatR;
namespace Safqa.Application;

public class ValidationBehavior<TReq, TRes>(IEnumerable<IValidator<TReq>> validators) : IPipelineBehavior<TReq, TRes> where TReq : notnull
{
    public async Task<TRes> Handle(TReq req, RequestHandlerDelegate<TRes> next, CancellationToken ct)
    {
        if (validators.Any())
        {
            var ctx = new ValidationContext<TReq>(req);
            var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(ctx, ct)))).SelectMany(r => r.Errors).ToList();
            if (failures.Count > 0) throw new ValidationException(failures);
        }
        return await next();
    }
}

public class ConvertLeadCommandValidator : AbstractValidator<ConvertLeadCommand>
{
    public ConvertLeadCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.Contact).NotNull();
        RuleFor(x => x.Company).NotNull();
        RuleFor(x => x.Contact.Id).NotEmpty().When(x => x.Contact is { Mode: MergeMode.Merge });
        RuleFor(x => x.Company.Id).NotEmpty().When(x => x.Company is { Mode: MergeMode.Merge });
        When(x => x.Deal != null, () =>
        {
            RuleFor(x => x.Deal!.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Deal!.Amount).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Deal!.StageId).NotEmpty();
        });
    }
}

public class MoveDealStageCommandValidator : AbstractValidator<MoveDealStageCommand>
{
    public MoveDealStageCommandValidator()
    {
        RuleFor(x => x.DealId).NotEmpty(); RuleFor(x => x.StageId).NotEmpty(); RuleFor(x => x.Version).GreaterThanOrEqualTo(1);
        When(x => x.Won?.Amount != null, () => RuleFor(x => x.Won!.Amount!.Value).GreaterThanOrEqualTo(0));
    }
}

public static class QuoteCalculator
{
    public record Line(decimal Quantity, decimal UnitPrice, decimal DiscountPct, decimal VatRate);
    public record Result(decimal Subtotal, decimal DiscountTotal, decimal VatTotal, decimal Total, List<decimal> LineTotals);
    static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static Result Calculate(IEnumerable<Line> lines)
    {
        decimal sub = 0, disc = 0, vat = 0; var totals = new List<decimal>();
        foreach (var l in lines)
        {
            var gross = R(l.Quantity * l.UnitPrice);
            var d = R(gross * l.DiscountPct / 100m);
            var v = R((gross - d) * l.VatRate / 100m);
            sub += gross; disc += d; vat += v; totals.Add(gross - d + v);
        }
        return new Result(sub, disc, vat, sub - disc + vat, totals);
    }
}
