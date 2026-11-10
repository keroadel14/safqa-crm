using FluentAssertions;
using Safqa.Application;
namespace Safqa.UnitTests;

public class QuoteCalculationTests
{
    static QuoteCalculator.Line L(decimal q, decimal p, decimal d, decimal v) => new(q, p, d, v);

    [Fact]
    public void Single_line_with_discount_and_15_percent_vat()
    {
        var r = QuoteCalculator.Calculate([L(2, 1000, 10, 15)]);   // gross 2000, discount 200, net 1800, VAT 270
        r.Subtotal.Should().Be(2000m); r.DiscountTotal.Should().Be(200m); r.VatTotal.Should().Be(270m); r.Total.Should().Be(2070m);
        r.LineTotals.Should().Equal(2070m);
    }

    [Fact]
    public void Fourteen_percent_vat_without_discount()
    {
        var r = QuoteCalculator.Calculate([L(1, 500, 0, 14)]);
        r.VatTotal.Should().Be(70m); r.Total.Should().Be(570m);
    }

    [Fact]
    public void Rounds_each_line_to_two_decimals_half_away_from_zero()
    {
        var r = QuoteCalculator.Calculate([L(3, 99.99m, 5, 15)]);  // gross 299.97, disc 14.9985→15.00, net 284.97, VAT 42.7455→42.75
        r.Subtotal.Should().Be(299.97m); r.DiscountTotal.Should().Be(15.00m); r.VatTotal.Should().Be(42.75m); r.Total.Should().Be(327.72m);
    }

    [Fact]
    public void Totals_are_sums_of_lines_and_identity_holds()
    {
        var r = QuoteCalculator.Calculate([L(1, 120000, 0, 15), L(40, 1500, 5, 15), L(12, 800, 0, 14)]);
        r.Total.Should().Be(r.Subtotal - r.DiscountTotal + r.VatTotal);
        r.LineTotals.Sum().Should().Be(r.Total);
        r.Subtotal.Should().Be(120000m + 60000m + 9600m);
    }

    [Fact]
    public void No_lines_gives_zero() => QuoteCalculator.Calculate([]).Total.Should().Be(0m);
}
