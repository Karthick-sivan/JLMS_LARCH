using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using JLMS.Api.Models;

namespace JLMS.Api.Services;

public class LoanReceiptPdfService
{
    private readonly string _uploadsRoot;

    public LoanReceiptPdfService()
    {
        _uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
    }

    public byte[] GenerateReceipt(Loan loan)
    {
        var customer = loan.Customer!;
        var customerPhoto = ReadPhotoBytes(customer.PhotoPath);
        var jewelPhoto = ReadPhotoBytes(loan.GroupPhotoPath);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ---- Letterhead ----
                    col.Item().Border(1).BorderColor(Colors.Grey.Darken1).Padding(14).Column(head =>
                    {
                        head.Item().AlignCenter().Text("Sri Masangaruppar Thunai").FontSize(9).Italic();

                        head.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem(3).Column(c =>
                            {
                                c.Item().Text("SRI SARAVANA BANKERS").FontSize(20).Bold().FontColor("#7a1f2b");
                                c.Item().Text("Government Approved").FontSize(9).FontColor(Colors.Blue.Darken2);
                                c.Item().Text("PBL. No. 01/2021-2022, Dt: 16.07.2021").FontSize(8.5f);
                                c.Item().Text("3/39, Mangulam Main Road, Poosaripatti, Madurai - 625122").FontSize(8.5f);
                                c.Item().Text("Ph: 9943155324").FontSize(8.5f);
                            });

                            row.RelativeItem(2).Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Loan No: ").SemiBold(); t.Span(loan.LoanNumber); });
                                c.Item().Text(t => { t.Span("Loan Amount: ").SemiBold(); t.Span($"Rs. {loan.LoanAmount:N2}"); });
                                c.Item().Text(t => { t.Span("Loan Date: ").SemiBold(); t.Span(loan.LoanDate?.ToString("dd-MM-yyyy") ?? "-"); });
                                c.Item().Text(t => { t.Span("Due Date: ").SemiBold(); t.Span(loan.MaturityDate?.ToString("dd-MM-yyyy") ?? "-"); });
                            });
                        });
                    });

                    // ---- Pledger details ----
                    col.Item().PaddingTop(10).Text(t => { t.Span("Customer Name: ").SemiBold(); t.Span(customer.CustomerName); });
                    col.Item().Text(t =>
                    {
                        t.Span("Address: ").SemiBold();
                        t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.State, customer.Pincode }
                            .Where(s => !string.IsNullOrWhiteSpace(s))));
                    });
                    col.Item().Text(t => { t.Span("Mobile: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });

                    // ---- Photos + jewel item table ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        row.ConstantItem(140).Column(c =>
                        {
                            c.Item().AlignCenter().Text("Item Photo").FontSize(8).SemiBold();
                            if (jewelPhoto != null) c.Item().Height(110).Image(jewelPhoto).FitArea();
                            else c.Item().Height(110).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("No Photo").FontSize(8);
                        });

                        row.RelativeItem().PaddingHorizontal(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(30);
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("S.No");
                                h.Cell().Element(HeaderCell).Text("Item");
                                h.Cell().Element(HeaderCell).Text("Purity");
                                h.Cell().Element(HeaderCell).Text("Wt (g)");
                            });

                            int sno = 1;
                            foreach (var ji in loan.JewelItems)
                            {
                                table.Cell().Element(BodyCell).Text(sno.ToString());
                                table.Cell().Element(BodyCell).Text(ji.JewelType?.JewelTypeName ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Purity ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.GrossWeightGrams.ToString("0.000"));
                                sno++;
                            }

                            static IContainer HeaderCell(IContainer c) =>
                                c.Background(Colors.Grey.Lighten2).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                            static IContainer BodyCell(IContainer c) =>
                                c.Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        });

                        row.ConstantItem(140).Column(c =>
                        {
                            c.Item().AlignCenter().Text("Customer Photo").FontSize(8).SemiBold();
                            if (customerPhoto != null) c.Item().Height(110).Image(customerPhoto).FitArea();
                            else c.Item().Height(110).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("No Photo").FontSize(8);
                        });
                    });

                    // ---- Amount details ----
                    col.Item().PaddingTop(12).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                        void Row(string label, string value)
                        {
                            table.Cell().Padding(3).Text(label).SemiBold();
                            table.Cell().Padding(3).AlignRight().Text(value);
                        }

                        Row("Loan Scheme", loan.LoanScheme?.SchemeName ?? "-");
                        Row("Interest Rate", $"{loan.InterestRatePct}% p.a.");
                        Row("Tenure", $"{loan.TenureMonths} months");
                        Row("Market Value", $"Rs. {loan.MarketValue:N2}");
                        Row("Eligible Amount", $"Rs. {loan.EligibleAmount:N2}");
                        Row("Loan Amount", $"Rs. {loan.LoanAmount:N2}");
                        Row("Processing Fee", $"Rs. {loan.ProcessingFee:N2}");
                        Row("Outstanding Principal", $"Rs. {loan.OutstandingPrincipal:N2}");
                        Row("Outstanding Interest", $"Rs. {loan.OutstandingInterest:N2}");
                    });

                    // ---- Signatures ----
                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().PaddingTop(2).Text("Pledger's Signature / Left Thumb Impression").FontSize(8);
                        });
                        row.ConstantItem(30);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().PaddingTop(2).Text("For Sri Saravana Bankers").FontSize(8);
                        });
                    });

                    // ---- Footer terms ----
                    col.Item().PaddingTop(16).Text(
                        "Office Hours: 7:00 AM to 8:00 PM, all days. Please renew this receipt every 6 months. " +
                        "If not redeemed within 1 year and 7 days from the pledge date, the pledged item is liable to be sold through auction.")
                        .FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                });
            });
        });

        return document.GeneratePdf();
    }

    private byte[]? ReadPhotoBytes(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var fullPath = Path.GetFullPath(Path.Combine(_uploadsRoot, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(_uploadsRoot), StringComparison.OrdinalIgnoreCase)) return null;
        return System.IO.File.Exists(fullPath) ? System.IO.File.ReadAllBytes(fullPath) : null;
    }
}