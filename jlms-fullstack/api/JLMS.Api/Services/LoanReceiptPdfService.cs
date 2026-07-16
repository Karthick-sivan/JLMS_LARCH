using JLMS.Api.DTOs;
using JLMS.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JLMS.Api.Services;

public class LoanReceiptPdfService
{
    private readonly string _uploadsRoot;

    public LoanReceiptPdfService()
    {
        _uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
    }

    // -------------------------------------------------------------------------
    // Shared layout helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Renders the centered company letterhead block inside a bordered box.
    /// Call as: col.Item().Element(c => RenderLetterhead(c, padding, fontSize));
    /// </summary>
    private static void RenderLetterhead(IContainer container, float padding = 10, float fontSize = 8)
    {
        container
            .Border(1).BorderColor(Colors.Grey.Darken1)
            .Padding(padding)
            .Column(head =>
            {
                head.Item().AlignCenter().Text("Sri Masangaruppar Thunai").FontSize(fontSize).Italic();
                head.Item().AlignCenter().Text("SRI SARAVANA BANKERS").FontSize(fontSize + 6).Bold().FontColor("#7a1f2b");
                head.Item().AlignCenter().Text("Government Approved | PBL. No. 01/2021-2022, Dt: 16.07.2021").FontSize(fontSize - 0.5f);
                head.Item().AlignCenter().Text("3/39, Mangulam Main Road, Poosaripatti, Madurai - 625122").FontSize(fontSize - 0.5f);
                head.Item().AlignCenter().Text("Ph: 9943155324").FontSize(fontSize - 0.5f);
            });
    }

    /// <summary>
    /// Renders the two-column signature row (pledger left, authorised signatory right).
    /// rightLabel examples: "For Sri Saravana Bankers", "Authorized Signatory - Sri Saravana Bankers"
    /// </summary>
    private static void RenderSignatureRow(ColumnDescriptor col, float paddingTop = 24,
        string rightLabel = "For Sri Saravana Bankers", float fontSize = 7.5f)
    {
        col.Item().PaddingTop(paddingTop).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(0.5f);
                c.Item().PaddingTop(2).Text("Pledger's Signature / Left Thumb Impression").FontSize(fontSize);
            });
            row.ConstantItem(20);
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(0.5f);
                c.Item().PaddingTop(2).Text(rightLabel).FontSize(fontSize);
            });
        });
    }

    /// <summary>
    /// Renders the red auction-warning footer line.
    /// </summary>
    private static void RenderAuctionFooter(ColumnDescriptor col, float fontSize = 7f)
    {
        col.Item().PaddingTop(10).Text(
            "Office Hours: 7:00 AM to 8:00 PM, all days. Keep this receipt safe. " +
            "Renew the receipt once every 6 months. " +
            "If not redeemed within 1 year and 7 days, the pledged item is liable to be sold through auction.")
            .FontSize(fontSize).FontColor(Colors.Red.Darken2);
    }

    // -------------------------------------------------------------------------
    // PDF generators
    // -------------------------------------------------------------------------

    /*
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
                    // A4 receipt keeps a split layout (company left, loan details right)
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

                    // ---- Pledger details + Customer Photo ----
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.ConstantItem(360).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Customer Name: ").SemiBold(); t.Span(customer.CustomerName); });
                            c.Item().Text(t =>
                            {
                                t.Span("Address: ").SemiBold();
                                t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.State, customer.Pincode }
                                    .Where(s => !string.IsNullOrWhiteSpace(s))));
                            });
                            c.Item().Text(t => { t.Span("Mobile: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });
                        });

                        row.RelativeItem();

                        row.ConstantItem(90).Column(c =>
                        {
                            c.Item().AlignCenter().Text("Customer Photo").FontSize(8).SemiBold();
                            if (customerPhoto != null) c.Item().Height(65).Image(customerPhoto).FitArea();
                            else c.Item().Height(65).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("No Photo").FontSize(8);
                        });
                    });

                    // ---- Item Photo + jewel item table ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        row.ConstantItem(130).Column(c =>
                        {
                            c.Item().AlignCenter().Text("Item Photo").FontSize(8).SemiBold();
                            if (jewelPhoto != null) c.Item().Height(100).Image(jewelPhoto).FitArea();
                            else c.Item().Height(100).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("No Photo").FontSize(8);
                        });

                        row.RelativeItem().PaddingLeft(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(40);
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
                        //Row("Market Value", $"Rs. {loan.MarketValue:N2}");
                        //Row("Eligible Amount", $"Rs. {loan.EligibleAmount:N2}");
                        Row("Loan Amount", $"Rs. {loan.LoanAmount:N2}");
                        Row("Processing Fee", $"Rs. {loan.ProcessingFee:N2}");
                        //Row("Outstanding Principal", $"Rs. {loan.OutstandingPrincipal:N2}");
                        //Row("Outstanding Interest", $"Rs. {loan.OutstandingInterest:N2}");
                    });

                    // ---- Signatures ----
                    RenderSignatureRow(col, paddingTop: 60, rightLabel: "For Sri Saravana Bankers", fontSize: 8);

                    // ---- Footer terms ----
                    RenderAuctionFooter(col, fontSize: 7.5f);
                });
            });
        });

        return document.GeneratePdf();
    }
    */

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

                // 1. MAIN CONTENT REGION (Root level of page)
                page.Content().Column(col =>
                {
                    // ---- Letterhead (Clean Centered Layout) ----
                    col.Item().Border(1).BorderColor(Colors.Grey.Darken1).Padding(12).Column(head =>
                    {
                        head.Item().AlignCenter().Text("Sri Masangaruppar Thunai").FontSize(9).Italic();
                        head.Item().AlignCenter().Text("SRI SARAVANA BANKERS").FontSize(22).Bold().FontColor("#7a1f2b");
                        head.Item().AlignCenter().Text("Government Approved | PBL. No. 01/2021-2022, Dt: 16.07.2021").FontSize(9).FontColor(Colors.Blue.Darken2);
                        head.Item().AlignCenter().Text("3/39, Mangulam Main Road, Poosaripatti, Madurai - 625122 | Ph: 9943155324").FontSize(8.5f);
                    });

                    // ---- Customer Details + Loan Details Brought Down + Customer Photo ----
                    col.Item().PaddingTop(12).Row(row =>
                    {
                        // Customer Block (Left)
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Customer Name: ").SemiBold(); t.Span(customer.CustomerName); });
                            c.Item().Text(t =>
                            {
                                t.Span("Address: ").SemiBold();
                                t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.State, customer.Pincode }
                                    .Where(s => !string.IsNullOrWhiteSpace(s))));
                            });
                            c.Item().Text(t => { t.Span("Mobile: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });

                            // Loan Details Block

                            c.Item().Text(t => { t.Span("Loan No: ").SemiBold(); t.Span(loan.LoanNumber); });
                            c.Item().Text(t => { t.Span("Loan Amount: ").SemiBold(); t.Span($"Rs. {loan.LoanAmount:N2}"); });
                            c.Item().Text(t => { t.Span("Loan Date: ").SemiBold(); t.Span(loan.LoanDate?.ToString("dd-MM-yyyy") ?? "-"); });
                            c.Item().Text(t => { t.Span("Due Date: ").SemiBold(); t.Span(loan.MaturityDate?.ToString("dd-MM-yyyy") ?? "-"); });
                        });

                        row.ConstantItem(30); // Spacer

                        
                        

                        // Photo Block (Right)
                        row.ConstantItem(85).Column(c =>
                        {
                            c.Item().AlignCenter().Text("Customer Photo").FontSize(8).SemiBold();
                            if (customerPhoto != null) c.Item().Height(65).Image(customerPhoto).FitArea();
                            else c.Item().Height(65).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("No Photo").FontSize(8);
                        });
                    });

                    // ---- Item Photo + jewel item table ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        row.ConstantItem(130).Column(c =>
                        {
                            c.Item().AlignCenter().Text("Item Photo").FontSize(8).SemiBold();
                            if (jewelPhoto != null) c.Item().Height(100).Image(jewelPhoto).FitArea();
                            else c.Item().Height(100).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("No Photo").FontSize(8);
                        });

                        row.RelativeItem().PaddingLeft(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(40);
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
                        Row("Loan Amount", $"Rs. {loan.LoanAmount:N2}");
                        Row("Processing Fee", $"Rs. {loan.ProcessingFee:N2}");
                    });
                });

                // 2. PINNED FOOTER REGION 
                page.Footer().Column(foot =>
                {
                    // ---- Signatures ----
                    RenderSignatureRow(foot, paddingTop: 20, rightLabel: "For Sri Saravana Bankers", fontSize: 8);

                    // ---- Footer terms ----
                    RenderAuctionFooter(foot, fontSize: 7.5f);
                });
            });
        });

        return document.GeneratePdf();
    }
    public byte[] GeneratePaymentReceipt(PaymentReceiptPdfDto r)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(14);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ---- Letterhead ----
                    col.Item().Element(c => RenderLetterhead(c, padding: 10, fontSize: 8));

                    // ---- Customer (left) + Receipt details (right, label/value columns aligned) ----
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        // Customer block — left half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(58); c.RelativeColumn(); });
                            t.Cell().Text("Customer:").SemiBold();
                            t.Cell().Text(r.CustomerName);
                            t.Cell().Text("Mobile:").SemiBold();
                            t.Cell().Text(r.Mobile ?? "-");
                        });

                        // Receipt details — right half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(68); c.RelativeColumn(); });
                            t.Cell().Text("Receipt No:").SemiBold();
                            t.Cell().Text(r.ReceiptNumber);
                            t.Cell().Text("Loan No:").SemiBold();
                            t.Cell().Text(r.LoanNo);
                            t.Cell().Text("Date:").SemiBold();
                            t.Cell().Text(r.TransactionDate.ToString("dd-MM-yyyy HH:mm"));
                            t.Cell().Text("Mode:").SemiBold();
                            t.Cell().Text(r.PaymentMode);
                            if (r.LoanScheme != null)
                            {
                                t.Cell().Text("Scheme:").SemiBold();
                                t.Cell().Text(r.LoanScheme);
                            }
                            if (r.MaturityDate.HasValue)
                            {
                                t.Cell().Text("Due Date:").SemiBold();
                                t.Cell().Text(r.MaturityDate.Value.ToString("dd-MM-yyyy"));
                            }
                        });
                    });

                    // ---- Amount details ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                        void AddRow(string label, string value, bool bold = false)
                        {
                            var labelText = table.Cell().Padding(3).Text(label);
                            var valueText = table.Cell().Padding(3).AlignRight().Text(value);
                            if (bold) { labelText.Bold(); valueText.Bold(); }
                        }

                        AddRow("Interest Paid", $"Rs. {r.InterestPaid:N2}");
                        AddRow("Principal Paid", $"Rs. {r.PrincipalPaid:N2}");
                        AddRow("Amount Received", $"Rs. {r.AmountReceived:N2}", bold: true);
                        AddRow("Remaining Interest", $"Rs. {r.RemainingInterest:N2}");
                        AddRow("Remaining Principal", $"Rs. {r.RemainingPrincipal:N2}");

                        var balanceAfter = r.RemainingInterest + r.RemainingPrincipal;
                        AddRow("Balance After Payment", $"Rs. {balanceAfter:N2}", bold: true);
                    });
                });

                // ---- Signatures + footer pinned to page bottom via page.Footer() ----
                page.Footer().Column(foot =>
                {
                    RenderSignatureRow(foot, paddingTop: 16, rightLabel: "For Sri Saravana Bankers");
                    RenderAuctionFooter(foot);
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateClosureReceipt(ClosureReceiptPdfDto r)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(14);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ---- Letterhead ----
                    col.Item().Element(c => RenderLetterhead(c, padding: 10, fontSize: 8));

                    // ---- Customer (left) + Receipt details (right) ----
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        // Customer block — left half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(58); c.RelativeColumn(); });
                            t.Cell().Text("Customer:").SemiBold();
                            t.Cell().Text(r.CustomerName);
                            t.Cell().Text("Mobile:").SemiBold();
                            t.Cell().Text(r.Mobile ?? "-");
                        });

                        // Receipt details — right half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(68); c.RelativeColumn(); });
                            t.Cell().Text("Receipt No:").SemiBold();
                            t.Cell().Text(r.ReceiptNumber);
                            t.Cell().Text("Loan No:").SemiBold();
                            t.Cell().Text(r.LoanNo);
                            t.Cell().Text("Scheme:").SemiBold();
                            t.Cell().Text(r.LoanScheme ?? "-");
                            t.Cell().Text("Closed Date:").SemiBold();
                            t.Cell().Text(r.TransactionDate.ToString("dd-MM-yyyy"));
                        });
                    });

                    // ---- Amount details ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                        void AddRow(string label, string value, bool bold = false)
                        {
                            var labelText = table.Cell().Padding(3).Text(label);
                            var valueText = table.Cell().Padding(3).AlignRight().Text(value);
                            if (bold) { labelText.Bold(); valueText.Bold(); }
                        }

                        AddRow("Outstanding Principal", $"Rs. {r.OutstandingPrincipal:N2}");
                        AddRow("Outstanding Interest", $"Rs. {r.OutstandingInterest:N2}");
                        AddRow("Other Charges", $"Rs. {r.OtherCharges:N2}");
                        AddRow("Grand Total Paid", $"Rs. {r.GrandTotal:N2}", bold: true);
                        AddRow("Status", "Closed");
                    });
                });

                page.Footer().Column(foot =>
                    {
                        // ---- Declaration ----
                        foot.Item().PaddingTop(12).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    {
                        ack.Item().Text("Declaration:").Bold().FontSize(7.5f);
                        ack.Item().Text("I hereby acknowledge receipt of all my pledged gold ornaments/articles in safe, intact, and original condition upon full closure of this loan account.").Italic().FontSize(7.5f);
                    });

                    // ---- Signatures ----
                    RenderSignatureRow(foot, paddingTop: 50, rightLabel: "Authorized Signatory - Sri Saravana Bankers");

                        // ---- Closure footer ----
                        foot.Item().PaddingTop(10).Text(
                        "Thank you for your business. This loan account has been fully settled and closed. " +
                        "No further outstanding amounts are due under this loan number.")
                        .FontSize(8f).FontColor(Colors.Green.Darken3).Bold();
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