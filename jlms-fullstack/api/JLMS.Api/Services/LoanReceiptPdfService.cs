using JLMS.Api.DTOs;
using JLMS.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JLMS.Api.Services;

public class LoanReceiptPdfService
{
    private readonly string _uploadsRoot;

    // IMPORTANT: Arial has no Tamil glyphs. Use a Tamil-capable font
    // (e.g. "Noto Sans Tamil") and register it once at app startup:
    //
    //   using var stream = File.OpenRead("Fonts/NotoSansTamil-Regular.ttf");
    //   QuestPDF.Drawing.FontManager.RegisterFont(stream);
    //
    // Download the font family from Google Fonts:
    // https://fonts.google.com/noto/specimen/Noto+Sans+Tamil
    // Then reference it below by its font family name, e.g. "Noto Sans Tamil".
    private const string TamilFont = "Noto Sans Tamil";

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
                head.Item().AlignCenter().Text("ஸ்ரீ மசான்குருப்பர் துணை").FontSize(fontSize).Italic();
                head.Item().AlignCenter().Text("ஸ்ரீ மீனாட்சி பேங்கர்ஸ்").FontSize(fontSize + 6).Bold().FontColor("#7a1f2b");
                head.Item().AlignCenter().Text("அரசு அங்கீகாரம் பெற்றது | பதிவு எண். 01/2021-2022, நாள்: 16.07.2021").FontSize(fontSize - 0.5f);
                head.Item().AlignCenter().Text("3/39, மங்குளம் மெயின் ரோடு, பூசாரிபட்டி, மதுரை - 625122").FontSize(fontSize - 0.5f);
                head.Item().AlignCenter().Text("தொ.பே: 9943155324").FontSize(fontSize - 0.5f);
            });
    }

    /// <summary>
    /// Renders the two-column signature row (pledger left, authorised signatory right).
    /// rightLabel examples: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக", "அங்கீகரிக்கப்பட்ட கையொப்பமிடுபவர் - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்"
    /// </summary>
    private static void RenderSignatureRow(ColumnDescriptor col, float paddingTop = 24,
        string rightLabel = "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக", float fontSize = 7.5f)
    {
        col.Item().PaddingTop(paddingTop).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(0.5f);
                c.Item().PaddingTop(2).Text("அடகு வைப்பவரின் கையொப்பம் / இடது கட்டைவிரல் ரேகை").FontSize(fontSize);
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
            "அலுவலக நேரம்: காலை 7:00 மணி முதல் இரவு 8:00 மணி வரை, அனைத்து நாட்களிலும். இந்த ரசீதை பத்திரமாக பாதுகாக்கவும். " +
            "6 மாதங்களுக்கு ஒருமுறை ரசீதை புதுப்பிக்கவும். " +
            "1 வருடம் 7 நாட்களுக்குள் மீட்கப்படாவிட்டால், அடகு வைக்கப்பட்ட பொருள் ஏலம் மூலம் விற்கப்படும்.")
            .FontSize(fontSize).FontColor(Colors.Red.Darken2);
    }

    // -------------------------------------------------------------------------
    // PDF generators
    // -------------------------------------------------------------------------

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
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(TamilFont));

                // 1. MAIN CONTENT REGION (Root level of page)
                page.Content().Column(col =>
                {
                    // ---- Letterhead (Clean Centered Layout) ----
                    col.Item().Border(1).BorderColor(Colors.Grey.Darken1).Padding(12).Column(head =>
                    {
                        head.Item().AlignCenter().Text("ஸ்ரீ மசான்குருப்பர் துணை").FontSize(9).Italic();
                        head.Item().AlignCenter().Text("ஸ்ரீ மீனாட்சி பேங்கர்ஸ்").FontSize(22).Bold().FontColor("#7a1f2b");
                        head.Item().AlignCenter().Text("அரசு அங்கீகாரம் பெற்றது | பதிவு எண். 01/2021-2022, நாள்: 16.07.2021").FontSize(9).FontColor(Colors.Blue.Darken2);
                        head.Item().AlignCenter().Text("3/39, மங்குளம் மெயின் ரோடு, பூசாரிபட்டி, மதுரை - 625122 | தொ.பே: 9943155324").FontSize(8.5f);
                    });

                    // ---- Customer Details + Loan Details Brought Down + Customer Photo ----
                    col.Item().PaddingTop(12).Row(row =>
                    {
                        // Customer Block (Left)
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("வாடிக்கையாளர் பெயர்: ").SemiBold(); t.Span(customer.CustomerName); });
                            c.Item().Text(t =>
                            {
                                t.Span("முகவரி: ").SemiBold();
                                t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.State, customer.Pincode }
                                    .Where(s => !string.IsNullOrWhiteSpace(s))));
                            });
                            c.Item().Text(t => { t.Span("மொபைல்: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });

                            // Loan Details Block

                            c.Item().Text(t => { t.Span("கடன் எண்: ").SemiBold(); t.Span(loan.LoanNumber); });
                            c.Item().Text(t => { t.Span("கடன் தொகை: ").SemiBold(); t.Span($"ரூ. {loan.LoanAmount:N2}"); });
                            c.Item().Text(t => { t.Span("கடன் திட்டம்: ").SemiBold(); t.Span(loan.LoanScheme?.SchemeName ?? "-"); });
                            c.Item().Text(t => { t.Span("கடன் தேதி: ").SemiBold(); t.Span(loan.LoanDate?.ToString("dd-MM-yyyy") ?? "-"); });
                            c.Item().Text(t => { t.Span("செலுத்த வேண்டிய தேதி: ").SemiBold(); t.Span(loan.MaturityDate?.ToString("dd-MM-yyyy") ?? "-"); });
                        });

                        row.ConstantItem(30); // Spacer

                        // Photo Block (Right)
                        row.ConstantItem(85).Column(c =>
                        {
                            c.Item().AlignCenter().Text("வாடிக்கையாளர் புகைப்படம்").FontSize(8).SemiBold();
                            if (customerPhoto != null) c.Item().Height(65).Image(customerPhoto).FitArea();
                            else c.Item().Height(65).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("புகைப்படம் இல்லை").FontSize(8);
                        });
                    });

                    // ---- Item Photo + jewel item table ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        row.ConstantItem(130).Column(c =>
                        {
                            c.Item().AlignCenter().Text("பொருள் புகைப்படம்").FontSize(8).SemiBold();
                            if (jewelPhoto != null) c.Item().Height(100).Image(jewelPhoto).FitArea();
                            else c.Item().Height(100).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("புகைப்படம் இல்லை").FontSize(8);
                        });

                        row.RelativeItem().PaddingLeft(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.5f);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("பொருள்");
                                h.Cell().Element(HeaderCell).Text("எண்ணம்");
                                h.Cell().Element(HeaderCell).Text("வடிவம்");
                                h.Cell().Element(HeaderCell).Text("வகை");
                                h.Cell().Element(HeaderCell).Text("எடை (கி)");
                            });

                            int sno = 1;
                            foreach (var ji in loan.JewelItems)
                            {
                                table.Cell().Element(BodyCell).Text(ji.JewelType?.JewelTypeName ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Quantity.ToString());
                                table.Cell().Element(BodyCell).Text(ji.Model ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Varient ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.GrossWeightGrams.ToString("0.000"));
                                sno++;
                            }

                            static IContainer HeaderCell(IContainer c) =>
                                c.Background(Colors.Grey.Lighten2).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                            static IContainer BodyCell(IContainer c) =>
                                c.Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        });
                    });
                });

                // 2. PINNED FOOTER REGION
                page.Footer().Column(foot =>
                {
                    // ---- Signatures ----
                    RenderSignatureRow(foot, paddingTop: 20, rightLabel: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக", fontSize: 8);

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
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(TamilFont));

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
                            t.Cell().Text("வாடிக்கையாளர்:").SemiBold();
                            t.Cell().Text(r.CustomerName);
                            t.Cell().Text("மொபைல்:").SemiBold();
                            t.Cell().Text(r.Mobile ?? "-");
                        });

                        // Receipt details — right half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(68); c.RelativeColumn(); });
                            t.Cell().Text("ரசீது எண்:").SemiBold();
                            t.Cell().Text(r.ReceiptNumber);
                            t.Cell().Text("கடன் எண்:").SemiBold();
                            t.Cell().Text(r.LoanNo);
                            t.Cell().Text("தேதி:").SemiBold();
                            t.Cell().Text(r.TransactionDate.ToString("dd-MM-yyyy HH:mm"));
                            t.Cell().Text("முறை:").SemiBold();
                            t.Cell().Text(r.PaymentMode);
                            if (r.LoanScheme != null)
                            {
                                t.Cell().Text("திட்டம்:").SemiBold();
                                t.Cell().Text(r.LoanScheme);
                            }
                            if (r.MaturityDate.HasValue)
                            {
                                t.Cell().Text("செலுத்த வேண்டிய தேதி:").SemiBold();
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

                        AddRow("செலுத்தப்பட்ட வட்டி", $"ரூ. {r.InterestPaid:N2}");
                        AddRow("செலுத்தப்பட்ட அசல்", $"ரூ. {r.PrincipalPaid:N2}");
                        AddRow("பெறப்பட்ட தொகை", $"ரூ. {r.AmountReceived:N2}", bold: true);
                        AddRow("மீதமுள்ள வட்டி", $"ரூ. {r.RemainingInterest:N2}");
                        AddRow("மீதமுள்ள அசல்", $"ரூ. {r.RemainingPrincipal:N2}");

                        var balanceAfter = r.RemainingInterest + r.RemainingPrincipal;
                        AddRow("செலுத்திய பின் மீதி", $"ரூ. {balanceAfter:N2}", bold: true);
                    });
                });

                // ---- Signatures + footer pinned to page bottom via page.Footer() ----
                page.Footer().Column(foot =>
                {
                    RenderSignatureRow(foot, paddingTop: 16, rightLabel: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக");
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
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(TamilFont));

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
                            t.Cell().Text("வாடிக்கையாளர்:").SemiBold();
                            t.Cell().Text(r.CustomerName);
                            t.Cell().Text("மொபைல்:").SemiBold();
                            t.Cell().Text(r.Mobile ?? "-");
                        });

                        // Receipt details — right half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(68); c.RelativeColumn(); });
                            t.Cell().Text("ரசீது எண்:").SemiBold();
                            t.Cell().Text(r.ReceiptNumber);
                            t.Cell().Text("கடன் எண்:").SemiBold();
                            t.Cell().Text(r.LoanNo);
                            t.Cell().Text("திட்டம்:").SemiBold();
                            t.Cell().Text(r.LoanScheme ?? "-");
                            t.Cell().Text("முடிவு தேதி:").SemiBold();
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

                        AddRow("நிலுவை அசல்", $"ரூ. {r.OutstandingPrincipal:N2}");
                        AddRow("நிலுவை வட்டி", $"ரூ. {r.OutstandingInterest:N2}");
                        AddRow("பிற கட்டணங்கள்", $"ரூ. {r.OtherCharges:N2}");
                        AddRow("மொத்தமாக செலுத்தப்பட்ட தொகை", $"ரூ. {r.GrandTotal:N2}", bold: true);
                        AddRow("நிலை", "முடிக்கப்பட்டது");
                    });
                });

                page.Footer().Column(foot =>
                {
                    // ---- Declaration ----
                    foot.Item().PaddingTop(12).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    {
                        ack.Item().Text("அறிவிப்பு:").Bold().FontSize(7.5f);
                        ack.Item().Text("இந்த கடன் கணக்கு முழுவதுமாக முடிக்கப்பட்டதன் மூலம், அடகு வைக்கப்பட்ட தங்க நகைகள்/பொருட்கள் அனைத்தையும் பாதுகாப்பாக, சேதமின்றி, மூல நிலையில் பெற்றுக்கொண்டதாக இதன் மூலம் உறுதிப்படுத்துகிறேன்.").Italic().FontSize(7.5f);
                    });

                    // ---- Signatures ----
                    RenderSignatureRow(foot, paddingTop: 50, rightLabel: "அங்கீகரிக்கப்பட்ட கையொப்பமிடுபவர் - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");

                    // ---- Closure footer ----
                    foot.Item().PaddingTop(10).Text(
                    "உங்கள் வணிகத்திற்கு நன்றி. இந்த கடன் கணக்கு முழுவதுமாக தீர்க்கப்பட்டு முடிக்கப்பட்டுள்ளது. " +
                    "இந்த கடன் எண்ணின் கீழ் மேலும் நிலுவைத் தொகை எதுவும் இல்லை.")
                    .FontSize(8f).FontColor(Colors.Green.Darken3).Bold();
                });

            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateClosureReceiptWithDetails(Loan loan, ClosureReceiptPdfDto r)
    {
        var customer = loan.Customer!;
        var customerPhoto = ReadPhotoBytes(customer.PhotoPath);
        var jewelPhoto = ReadPhotoBytes(loan.GroupPhotoPath);
        var closurePhoto = ReadPhotoBytes(loan.ClosePhotoPath);   // ★ NEW

        var document = Document.Create(container =>
        {
            // ================= PAGE 1 — same layout as GenerateReceipt() =================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(TamilFont));

                page.Content().Column(col =>
                {
                    col.Item().Border(1).BorderColor(Colors.Grey.Darken1).Padding(12).Column(head =>
                    {
                        head.Item().AlignCenter().Text("ஸ்ரீ மசான்குருப்பர் துணை").FontSize(9).Italic();
                        head.Item().AlignCenter().Text("ஸ்ரீ மீனாட்சி பேங்கர்ஸ்").FontSize(22).Bold().FontColor("#7a1f2b");
                        head.Item().AlignCenter().Text("அரசு அங்கீகாரம் பெற்றது | பதிவு எண். 01/2021-2022, நாள்: 16.07.2021").FontSize(9).FontColor(Colors.Blue.Darken2);
                        head.Item().AlignCenter().Text("3/39, மங்குளம் மெயின் ரோடு, பூசாரிபட்டி, மதுரை - 625122 | தொ.பே: 9943155324").FontSize(8.5f);
                    });

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("வாடிக்கையாளர் பெயர்: ").SemiBold(); t.Span(customer.CustomerName); });
                            c.Item().Text(t =>
                            {
                                t.Span("முகவரி: ").SemiBold();
                                t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.State, customer.Pincode }
                                    .Where(s => !string.IsNullOrWhiteSpace(s))));
                            });
                            c.Item().Text(t => { t.Span("மொபைல்: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });

                            c.Item().Text(t => { t.Span("கடன் எண்: ").SemiBold(); t.Span(loan.LoanNumber); });
                            c.Item().Text(t => { t.Span("கடன் தொகை: ").SemiBold(); t.Span($"ரூ. {loan.LoanAmount:N2}"); });
                            c.Item().Text(t => { t.Span("கடன் திட்டம்: ").SemiBold(); t.Span(loan.LoanScheme?.SchemeName ?? "-"); });
                            c.Item().Text(t => { t.Span("கடன் தேதி: ").SemiBold(); t.Span(loan.LoanDate?.ToString("dd-MM-yyyy") ?? "-"); });
                            c.Item().Text(t => { t.Span("செலுத்த வேண்டிய தேதி: ").SemiBold(); t.Span(loan.MaturityDate?.ToString("dd-MM-yyyy") ?? "-"); });
                        });

                        row.ConstantItem(30);

                        row.ConstantItem(85).Column(c =>
                        {
                            c.Item().AlignCenter().Text("வாடிக்கையாளர் புகைப்படம்").FontSize(8).SemiBold();
                            if (customerPhoto != null) c.Item().Height(65).Image(customerPhoto).FitArea();
                            else c.Item().Height(65).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("புகைப்படம் இல்லை").FontSize(8);
                        });
                    });

                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        row.ConstantItem(130).Column(c =>
                        {
                            c.Item().AlignCenter().Text("பொருள் புகைப்படம்").FontSize(8).SemiBold();
                            if (jewelPhoto != null) c.Item().Height(100).Image(jewelPhoto).FitArea();
                            else c.Item().Height(100).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle().Text("புகைப்படம் இல்லை").FontSize(8);
                        });

                        row.RelativeItem().PaddingLeft(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.5f);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("பொருள்");
                                h.Cell().Element(HeaderCell).Text("எண்ணிக்கை");
                                h.Cell().Element(HeaderCell).Text("வடிவம்");
                                h.Cell().Element(HeaderCell).Text("வகை");
                                h.Cell().Element(HeaderCell).Text("எடை (கி)");
                            });

                            foreach (var ji in loan.JewelItems)
                            {
                                table.Cell().Element(BodyCell).Text(ji.JewelType?.JewelTypeName ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Quantity.ToString());
                                table.Cell().Element(BodyCell).Text(ji.Model ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Varient ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.GrossWeightGrams.ToString("0.000"));
                            }

                            static IContainer HeaderCell(IContainer c) =>
                                c.Background(Colors.Grey.Lighten2).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                            static IContainer BodyCell(IContainer c) =>
                                c.Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        });
                    });
                });

                page.Footer().Column(foot =>
                {
                    RenderSignatureRow(foot, paddingTop: 20, rightLabel: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக", fontSize: 8);
                    RenderAuctionFooter(foot, fontSize: 7.5f);
                });
            });

            // ================= PAGE 2 — Closure confirmation + photo =================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(TamilFont));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().PaddingTop(10)
                        .Text("கடன் முடிவு உறுதிப்படுத்தல்").FontSize(16).Bold().FontColor("#7a1f2b");

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            var closedAt = loan.ClosedAt ?? r.TransactionDate;
                            c.Item().Text(t => { t.Span("முடிவு தேதி: ").SemiBold().FontSize(11); t.Span(closedAt.ToString("dd-MM-yyyy")).FontSize(11); });
                            c.Item().PaddingTop(6).Text(t => { t.Span("முடிவு நேரம்: ").SemiBold().FontSize(11); t.Span(closedAt.ToString("hh:mm tt")).FontSize(11); });
                        });

                        row.ConstantItem(30); // spacer

                        row.ConstantItem(130).Column(c =>
                        {
                            c.Item().AlignCenter().Text("முடிவின் போது வாடிக்கையாளர் புகைப்படம்").FontSize(9).SemiBold();

                            if (closurePhoto != null)
                            {
                                c.Item()
                                    .PaddingTop(6)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Width(130)
                                    .Height(130)
                                    .Image(closurePhoto)
                                    .FitUnproportionally();
                            }
                            else
                                c.Item().PaddingTop(6).Height(130).Width(130).Background(Colors.Grey.Lighten3)
                                 .AlignCenter().AlignMiddle().Text("முடிவு புகைப்படம் இல்லை").FontSize(8);
                        });
                    });

                    col.Item().PaddingTop(24).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    {
                        ack.Item().Text("அறிவிப்பு:").Bold().FontSize(7.5f);
                        ack.Item().Text("இந்த கடன் கணக்கு முழுவதுமாக முடிக்கப்பட்டதன் மூலம், அடகு வைக்கப்பட்ட தங்க நகைகள்/பொருட்கள் அனைத்தையும் பாதுகாப்பாக, சேதமின்றி, மூல நிலையில் பெற்றுக்கொண்டதாக இதன் மூலம் உறுதிப்படுத்துகிறேன்.").Italic().FontSize(7.5f);
                    });
                });

                page.Footer().Column(foot =>
                {
                    RenderSignatureRow(foot, paddingTop: 20, rightLabel: "அங்கீகரிக்கப்பட்ட கையொப்பமிடுபவர் - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");

                    foot.Item().PaddingTop(10).Text(
                        "உங்கள் வணிகத்திற்கு நன்றி. இந்த கடன் கணக்கு முழுவதுமாக தீர்க்கப்பட்டு முடிக்கப்பட்டுள்ளது. " +
                        "இந்த கடன் எண்ணின் கீழ் மேலும் நிலுவைத் தொகை எதுவும் இல்லை.")
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
