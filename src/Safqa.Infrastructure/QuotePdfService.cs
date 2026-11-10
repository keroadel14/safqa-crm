using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Safqa.Domain;
namespace Safqa.Infrastructure;

public class QuotePdfService
{
    const string Brand = "#0E7490";
    static readonly Dictionary<string, string[]> L = new()
    {
        ["en"] = ["Quotation", "Customer", "Deal", "Valid until", "Description", "Qty", "Unit price", "Disc %", "VAT %", "Total", "Subtotal", "Discount", "VAT", "Grand total", "Terms & conditions", "Status"],
        ["ar"] = ["عرض سعر", "العميل", "الصفقة", "صالح حتى", "الوصف", "الكمية", "سعر الوحدة", "خصم %", "ضريبة %", "الإجمالي", "المجموع الفرعي", "الخصم", "ضريبة القيمة المضافة", "الإجمالي الكلي", "الشروط والأحكام", "الحالة"],
    };
    static string M(decimal v, string cur) => v.ToString("N2", CultureInfo.InvariantCulture) + " " + cur;

    public byte[] Generate(Quote q, string dealName, string? companyName, string orgName, string lang)
    {
        lang = lang == "ar" ? "ar" : "en"; var t = L[lang]; var rtl = lang == "ar";
        return Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.A4); p.Margin(36);
            p.DefaultTextStyle(x => x.FontFamily("Noto Sans Arabic", "Inter", "Arial").FontSize(10));
            if (rtl) p.ContentFromRightToLeft();

            p.Header().PaddingBottom(12).BorderBottom(2).BorderColor(Brand).Row(r =>
            {
                r.RelativeItem().Text(orgName).FontSize(20).Bold().FontColor(Brand);
                r.ConstantItem(170).Column(c =>
                {
                    c.Item().Text(t[0]).FontSize(14).SemiBold();
                    c.Item().Text(q.Number).FontSize(12).Bold();
                    c.Item().Text($"{t[3]}: {q.ValidUntil:yyyy-MM-dd}");
                });
            });

            p.Content().PaddingVertical(14).Column(col =>
            {
                col.Spacing(12);
                col.Item().Column(c =>
                {
                    c.Item().Text($"{t[1]}: {companyName ?? "-"}").SemiBold();
                    c.Item().Text($"{t[2]}: {dealName}");
                });
                col.Item().Table(tb =>
                {
                    tb.ColumnsDefinition(c => { c.ConstantColumn(22); c.RelativeColumn(4); c.ConstantColumn(38); c.ConstantColumn(70); c.ConstantColumn(44); c.ConstantColumn(40); c.ConstantColumn(76); });
                    string[] heads = ["#", t[4], t[5], t[6], t[7], t[8], t[9]];
                    tb.Header(h => { foreach (var x in heads) h.Cell().Background(Brand).Padding(5).Text(x).FontColor(Colors.White).SemiBold(); });
                    foreach (var l in q.Lines.OrderBy(x => x.Position))
                    {
                        IContainer Cell(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                        tb.Cell().Element(Cell).Text((l.Position + 1).ToString());
                        tb.Cell().Element(Cell).Text(l.Description);
                        tb.Cell().Element(Cell).Text(l.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
                        tb.Cell().Element(Cell).Text(l.UnitPrice.ToString("N2", CultureInfo.InvariantCulture));
                        tb.Cell().Element(Cell).Text(l.DiscountPct.ToString("0.##", CultureInfo.InvariantCulture));
                        tb.Cell().Element(Cell).Text(l.VatRate.ToString("0.##", CultureInfo.InvariantCulture));
                        tb.Cell().Element(Cell).Text(l.LineTotal.ToString("N2", CultureInfo.InvariantCulture));
                    }
                });
                col.Item().AlignRight().Width(230).Column(c =>
                {
                    void Row(string label, string val, bool bold = false) => c.Item().PaddingVertical(2).Row(r =>
                    {
                        var a = r.RelativeItem().Text(label); var b = r.ConstantItem(100).AlignRight().Text(val);
                        if (bold) { a.Bold().FontColor(Brand); b.Bold().FontColor(Brand); }
                    });
                    Row(t[10], M(q.Subtotal, q.Currency)); Row(t[11], "-" + M(q.DiscountTotal, q.Currency)); Row(t[12], M(q.VatTotal, q.Currency));
                    c.Item().LineHorizontal(1).LineColor(Brand);
                    Row(t[13], M(q.Total, q.Currency), true);
                });
                if (!string.IsNullOrWhiteSpace(q.Terms))
                    col.Item().Column(c => { c.Item().Text(t[14]).SemiBold(); c.Item().Text(q.Terms).FontSize(9).FontColor(Colors.Grey.Darken2); });
            });

            p.Footer().AlignCenter().Text(x => { x.Span(orgName + "  ·  ").FontSize(8); x.CurrentPageNumber().FontSize(8); x.Span(" / ").FontSize(8); x.TotalPages().FontSize(8); });
        })).GeneratePdf();
    }
}
