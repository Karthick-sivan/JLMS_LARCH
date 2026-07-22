using JLMS.Api.DTOs;
using JLMS.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static System.Net.Mime.MediaTypeNames;

namespace JLMS.Api.Services;

public class LoanReceiptPdfService
{
    private readonly string _uploadsRoot;
    private readonly byte[]? _logoBytes;

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
        _logoBytes = LoadLogoBytes();
    }

    private static byte[]? LoadLogoBytes()
    {
        var assetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var path = Path.Combine(assetsRoot, "company-logo" + ext);
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }
        return null;
    }
    // -------------------------------------------------------------------------
    // Shared layout helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Renders the centered company letterhead block inside a bordered box.
    /// Call as: col.Item().Element(c => RenderLetterhead(c, padding, fontSize));
    /// </summary>
    private void RenderLetterhead(IContainer container, float padding = 10, float fontSize = 8)
    {
        container
            .Border(1).BorderColor(Colors.Grey.Darken1)
            .Padding(padding)
            .Row(row =>
            {
                // ---- Logo (left) ----
                row.ConstantItem(50).AlignMiddle().Element(e =>
                {
                    if (_logoBytes != null)
                        e.Image(_logoBytes).FitArea();
                });

                // ---- Company block (center, takes remaining width) ----
                row.RelativeItem().Column(head =>
                {
                    head.Item().AlignCenter().Text("ஸ்ரீ மாசங்கருப்பர்   துணை").FontSize(fontSize).Italic();
                    head.Item().AlignCenter().Text("ஸ்ரீ மீனாட்சி பேங்கர்ஸ்").FontSize(fontSize + 6).Bold().FontColor("#7a1f2b");
                    head.Item().AlignCenter().Text("அரசு அங்கீகாரம் பெற்றது | பதிவு எண்., நாள்: 16.07.2021").FontSize(fontSize - 0.5f);
                    head.Item().AlignCenter().Text("மாங்குளம் மெயின் ரோடு,ராமராஜபுரம், மதுரை - 625122").FontSize(fontSize - 0.5f);
                    head.Item().AlignCenter().Text("தொலைபேசி எண் : 7550098326").FontSize(fontSize - 0.5f);
                });

                // ---- Spacer to visually balance the logo on the left (keeps text centered) ----
                row.ConstantItem(50);
            });
    }

    /// <summary>
    /// Renders the letterhead box PLUS a right-hand info column (loan number / amount / date),
    /// matching the "அடகு எண் / அடகு ரூபாய் / அடகு தேதி" layout shown in the sample receipt.
    /// Call as: col.Item().Element(c => RenderLetterheadWithInfo(c, loan.LoanNumber, loan.LoanAmount, loan.LoanDate, loan.MaturityDate));
    /// </summary>
    private void RenderLetterheadWithInfo(IContainer container, string? loanNumber, decimal loanAmount, DateTime? loanDate,
        DateTime? maturityDate, float padding = 12, float fontSize = 9)
    {
        container
            .Border(1).BorderColor(Colors.Grey.Darken1)
            .Padding(padding)
            .Row(row =>
            {
                // ---- Logo (left) ---- ★ NEW
                row.ConstantItem(60).AlignMiddle().Element(e =>
                {
                    if (_logoBytes != null)
                        e.Image(_logoBytes).FitArea();
                });

                row.ConstantItem(8);

                // ---- Company block (center) ----
                row.RelativeItem(3).Column(head =>
                {
                    head.Item().AlignCenter().Text("ஸ்ரீ மாசங்கருப்பர்  துணை").FontSize(fontSize).Italic();
                    head.Item().AlignCenter().Text("ஸ்ரீ மீனாட்சி பேங்கர்ஸ்").FontSize(fontSize + 13).Bold().FontColor("#7a1f2b");
                    head.Item().AlignCenter().Text("அரசு அங்கீகாரம் பெற்றது | பதிவு எண்., நாள்: 16.07.2021").FontSize(fontSize).FontColor(Colors.Blue.Darken2);
                    head.Item().AlignCenter().Text("மாங்குளம்  மெயின் ரோடு, ராமராஜபுரம், மதுரை - 625122").FontSize(fontSize - 0.5f);
                    head.Item().AlignCenter().Text("தொலைபேசி எண் : 7550098326").FontSize(fontSize - 0.5f);
                });

                row.ConstantItem(10);

                // ---- Loan number / Amount / Date block (right, unchanged) ----
                row.RelativeItem(1).AlignMiddle().Column(info =>
                {
                    info.Item().Text(t =>
                    {
                        t.Span("அடகு எண்: ").SemiBold().FontSize(8.5f);
                        t.Span(loanNumber ?? "-").FontSize(8.5f);
                    });
                    info.Item().PaddingTop(3).Text(t =>
                    {
                        t.Span("அசல் தொகை: ").SemiBold().FontSize(8.5f);
                        t.Span($"ரூ. {loanAmount:N2}").FontSize(8.5f);
                    });
                    info.Item().PaddingTop(3).Text(t =>
                    {
                        t.Span("அடகு தேதி: ").SemiBold().FontSize(8.5f);
                        t.Span(loanDate?.ToString("dd-MM-yyyy") ?? DateTime.Now.ToString("dd-MM-yyyy")).FontSize(8.5f);
                    });
                    info.Item().PaddingTop(3).Text(t =>
                    {
                        t.Span("மீட்கப்பட வேண்டிய தேதி: ").SemiBold().FontSize(8.5f);
                        t.Span(maturityDate?.ToString("dd-MM-yyyy") ?? "-").FontSize(8.5f);
                    });
                });
            });
    }
    /// <summary>
    /// Renders a labelled, bordered photo box (used for jewel photo / customer photo),
    /// matching the framed-photo look in the sample receipt.
    /// </summary>
    private static void RenderPhotoBox(IContainer container, string label, byte[]? photoBytes,
        float width = 85, float height = 95, float labelFontSize = 8)
    {
        container.Width(width).Column(c =>
        {
            c.Item().AlignCenter().Text(label).FontSize(labelFontSize).SemiBold();
            c.Item().PaddingTop(3)
                .Border(1).BorderColor(Colors.Grey.Darken2)
                .Background(Colors.White)
                .Width(width).Height(height)
                .Padding(2)
                .AlignCenter().AlignMiddle()
                .Element(e =>
                {
                    if (photoBytes != null)
                        e.Image(photoBytes).FitArea();
                    else
                        e.Text("புகைப்படம் இல்லை").FontSize(labelFontSize - 0.5f).FontColor(Colors.Grey.Darken1);
                });
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
    //private static void RenderAuctionFooter(ColumnDescriptor col, float fontSize = 7f)
    //{
    //    col.Item().PaddingTop(10).Text(
    //        "அலுவலக நேரம்: காலை 7:00 மணி முதல் இரவு 8:00 மணி வரை, அனைத்து நாட்களிலும். இந்த ரசீதை பத்திரமாக பாதுகாக்கவும். " +
    //        "6 மாதங்களுக்கு ஒருமுறை ரசீதை புதுப்பிக்கவும். " +
    //        "1 வருடம் 7 நாட்களுக்குள் மீட்கப்படாவிட்டால், அடகு வைக்கப்பட்ட பொருள் ஏலம் மூலம் விற்கப்படும்.")
    //        .FontSize(fontSize).FontColor(Colors.Red.Darken2);
    //}
    private static void RenderAuctionFooter(ColumnDescriptor col, float fontSize = 7f)
    {
        col.Item().PaddingTop(10).Text(
            "ஆபிஸ் நேரம் : காலை 7 மணி முதல் இரவு 8 மணி வரை அனைத்து நாட்களும் செயல்படும். இந்த ரசீதை நகையை போல் பாதுகாக்கவும் " +
            "6 மாதத்திற்கு ஒரு முறை ரசீதை புதுப்பித்துக்கொள்ளவும் . இல்லை எனில் 1 வருடம் 7 நாட்களுக்குள் திருப்ப தவறினால் அடகு பொருள் ஏலம் மூலம் விற்கப்படும்.")
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
                    // ---- Letterhead (with loan number / amount / date on the right, like the sample) ----
                    col.Item().Element(c => RenderLetterheadWithInfo(c, loan.LoanNumber, loan.LoanAmount, loan.LoanDate, loan.MaturityDate));

                    // ---- Customer Details (full width text block) ----
                    col.Item().PaddingTop(12).Column(c =>
                    {
                        c.Item().Text(t => { t.Span("வாடிக்கையாளர் பெயர்: ").SemiBold(); t.Span(customer.CustomerName); });
                        c.Item().Text(t =>
                        {
                            t.Span("முகவரி: ").SemiBold();
                            t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.Pincode }
                                .Where(s => !string.IsNullOrWhiteSpace(s))));
                        });
                        c.Item().Text(t => { t.Span("தொலைபேசி: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });
                        //c.Item().Text(t => { t.Span("கடன் திட்டம்: ").SemiBold(); t.Span(loan.LoanScheme?.SchemeName ?? "-"); });
                        // NOTE: அடகு எண் / அசல் தொகை / தேதி are shown once, in the letterhead box above — not repeated here.
                    });

                    // ---- Item Photo + jewel item table + Customer Photo (2nd box, right side) ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        // Jewel photo (left) — kept small so the table gets more room
                        row.ConstantItem(75).Element(e =>
                            RenderPhotoBox(e, "பொருள் படம்", jewelPhoto, width: 68, height: 78, labelFontSize: 7));

                        // Jewel item table (middle)
                        row.RelativeItem().PaddingHorizontal(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2f);
                                c.RelativeColumn(1.9f);   // wider so "எண்ணிக்கை" fits on one line
                                c.RelativeColumn(1.6f);
                                c.RelativeColumn(1.6f);
                                c.RelativeColumn(1.1f);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("பொருள்");
                                h.Cell().Element(HeaderCell).Text("எண்ணிக்கை").FontSize(8);
                                h.Cell().Element(HeaderCell).Text("வடிவம்");
                                h.Cell().Element(HeaderCell).Text("வகை");
                                h.Cell().Element(HeaderCell).Text("எடை (கி)");
                            });

                            int sno = 1;
                            foreach (var ji in loan.JewelItems)
                            {
                                var text = (ji.Varient ?? "-")
    .Replace(" ", "\n");
                                table.Cell().Element(BodyCell).Text(ji.JewelType?.JewelTypeName ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Quantity.ToString());
                                table.Cell().Element(BodyCell).Text(ji.Model ?? "-");
                                //table.Cell().Element(BodyCell).Text(ji.Varient ?? "-");
                                table.Cell()
    .Element(BodyCell)
    .Text(text);
                                table.Cell().Element(BodyCell).Text(ji.GrossWeightGrams.ToString("0.000"));
                                sno++;
                            }

                            // Full grid borders (all sides). IMPORTANT: Border() must come BEFORE
                            // Padding() so the border sits at the cell's outer edge and touches the
                            // neighboring cell's border directly — no gap between cells.
                            static IContainer HeaderCell(IContainer c) =>
                                c.Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten2).Padding(4);
                            static IContainer BodyCell(IContainer c) =>
                                c.Border(1).BorderColor(Colors.Grey.Darken1).Padding(4);
                        });

                        // Customer photo (right)
                        row.ConstantItem(90).Element(e =>
                            RenderPhotoBox(e, "புகைப்படம்", customerPhoto, width: 80, height: 95));
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
                            t.Cell().Text("தொலைபேசி:").SemiBold();
                            t.Cell().Text(r.Mobile ?? "-");
                        });

                        // Receipt details — right half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(68); c.RelativeColumn(); });
                            t.Cell().Text("ரசீது எண்:").SemiBold();
                            t.Cell().Text(r.ReceiptNumber);
                            t.Cell().Text("எண்:").SemiBold();
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
                                t.Cell().Text("மீட்கப்பட வேண்டிய தேதி:").SemiBold();
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
                            t.Cell().Text("தொலைபேசி:").SemiBold();
                            t.Cell().Text(r.Mobile ?? "-");
                        });

                        // Receipt details — right half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(68); c.RelativeColumn(); });
                            //t.Cell().Text("ரசீது எண்:").SemiBold();
                            //t.Cell().Text(r.ReceiptNumber);
                            t.Cell().Text("எண்:").SemiBold();
                            t.Cell().Text(r.LoanNo);
                            //t.Cell().Text("திட்டம்:").SemiBold();
                            //t.Cell().Text(r.LoanScheme ?? "-");
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
                    //foot.Item().PaddingTop(12).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    //{
                    //    ack.Item().Text("அறிவிப்பு:").Bold().FontSize(7.5f);
                    //    ack.Item().Text("இந்த கணக்கு முழுவதுமாக முடிக்கப்பட்டதன் மூலம், அடகு வைக்கப்பட்ட தங்க நகைகள்/பொருட்கள் அனைத்தையும் பாதுகாப்பாக, சேதமின்றி, மூல நிலையில் பெற்றுக்கொண்டதாக இதன் மூலம் உறுதிப்படுத்துகிறேன்.").Italic().FontSize(7.5f);
                    //});
                    foot.Item().PaddingTop(12).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    {
                        ack.Item().Text("1. ஒவ்வொரு நகையும் 1 வருடத் தவணைக்குள் மீட்கப்பட வேண்டும். மீட்க தவறினால் அடகு மீட்க ஒப்புக்கொண்ட கால அளவுக்கு மேல் 7 நாட்களுக்குள் மேற்படி மீட்பு அங்கீகரிக்கப்படும். மேலும் தவறினால் அடகு வைத்தவருக்கு நோட்டீஸ்  கொடுத்துவிட்டு அதன் பிறகு பகிரங்க ஏலத்தில் நகைகள் ஏலம் போடப்படும்.").FontSize(7.5f);
                        ack.Item().PaddingTop(4).Text("2. வீடு மாறினாலோ அல்லது ரசீது தவறினாலோ உடனடியாக தெரிவிக்க வேண்டும். தவறினால் நாங்கள்  ஜவாப்தாரியல்ல.").FontSize(7.5f);
                        ack.Item().PaddingTop(4).Text("3. அடகு பொருட்களின் விலை மதிப்பு குறைந்தால் (Depreciation of Price) அடகு வைத்தவர் உடனே மார்ஜின் தொகை கட்ட வேண்டும். தவறினால் அடகு வைத்தவருக்கு தெரியப்படுத்தி அடகு பொருட்கள் பகிரங்க ஏலம் போடப்படும்.").FontSize(7.5f);
                        ack.Item().PaddingTop(4).Text("4. இதில் கண்ட அசல் வட்டி தொகைகளை செலுத்தி முன் பக்கத்தில் கண்ட அடகு பொருட்களை சரிபார்த்து பெற்று கொண்டேன் .").FontSize(7.5f);
                    });
                    // ---- Signatures ----
                    RenderSignatureRow(foot, paddingTop: 50, rightLabel: "அங்கீகரிக்கப்பட்ட கையொப்பமிடுபவர் - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");

                    // ---- Closure footer ----
                    foot.Item().PaddingTop(10).Text(
                    "உங்கள் வணிகத்திற்கு நன்றி. இந்த கணக்கு முழுவதுமாக தீர்க்கப்பட்டு முடிக்கப்பட்டுள்ளது. " +
                    "இந்த எண்ணின் கீழ் மேலும் நிலுவைத் தொகை எதுவும் இல்லை.")
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
                    // ---- Letterhead (with loan number / amount / date on the right, like the sample) ----
                    col.Item().Element(c => RenderLetterheadWithInfo(c, loan.LoanNumber, loan.LoanAmount, loan.LoanDate, loan.MaturityDate));

                    // ---- Customer Details (full width text block) ----
                    col.Item().PaddingTop(12).Column(c =>
                    {
                        c.Item().Text(t => { t.Span("வாடிக்கையாளர் பெயர்: ").SemiBold(); t.Span(customer.CustomerName); });
                        c.Item().Text(t =>
                        {
                            t.Span("முகவரி: ").SemiBold();
                            t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.Pincode }
                                .Where(s => !string.IsNullOrWhiteSpace(s))));
                        });
                        c.Item().Text(t => { t.Span("தொலைபேசி: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });
                        //c.Item().Text(t => { t.Span("கடன் திட்டம்: ").SemiBold(); t.Span(loan.LoanScheme?.SchemeName ?? "-"); });
                        // NOTE: அடகு எண் / அசல் தொகை / தேதி are shown once, in the letterhead box above — not repeated here.
                    });

                    // ---- Item Photo + jewel item table + Customer Photo (2nd box, right side) ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        // Jewel photo (left) — kept small so the table gets more room
                        row.ConstantItem(75).Element(e =>
                            RenderPhotoBox(e, "பொருள் படம்", jewelPhoto, width: 68, height: 78, labelFontSize: 7));

                        // Jewel item table (middle)
                        row.RelativeItem().PaddingHorizontal(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2f);
                                c.RelativeColumn(1.9f);   // wider so "எண்ணிக்கை" fits on one line
                                c.RelativeColumn(1.6f);
                                c.RelativeColumn(1.6f);
                                c.RelativeColumn(1.1f);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("பொருள்");
                                h.Cell().Element(HeaderCell).Text("எண்ணிக்கை").FontSize(8);
                                h.Cell().Element(HeaderCell).Text("வடிவம்");
                                h.Cell().Element(HeaderCell).Text("வகை");
                                h.Cell().Element(HeaderCell).Text("எடை (கி)");
                            });

                            foreach (var ji in loan.JewelItems)
                            {
                                var text = (ji.Varient ?? "-")
                                 .Replace(" ", "\n");
                                table.Cell().Element(BodyCell).Text(ji.JewelType?.JewelTypeName ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Quantity.ToString());
                                table.Cell().Element(BodyCell).Text(ji.Model ?? "-");
                                //table.Cell().Element(BodyCell).Text(ji.Varient ?? "-");
                                table.Cell()
                                      .Element(BodyCell)
                                      .Text(text);
                                table.Cell().Element(BodyCell).Text(ji.GrossWeightGrams.ToString("0.000"));
                            }

                            // Full grid borders (all sides). IMPORTANT: Border() must come BEFORE
                            // Padding() so the border sits at the cell's outer edge and touches the
                            // neighboring cell's border directly — no gap between cells.
                            static IContainer HeaderCell(IContainer c) =>
                                c.Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten2).Padding(4);
                            static IContainer BodyCell(IContainer c) =>
                                c.Border(1).BorderColor(Colors.Grey.Darken1).Padding(4);
                        });

                        // Customer photo (right)
                        row.ConstantItem(90).Element(e =>
                            RenderPhotoBox(e, " புகைப்படம்", customerPhoto, width: 80, height: 95));
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
                        .Text("கணக்கு  முடிவு உறுதிப்படுத்தல்").FontSize(16).Bold().FontColor("#7a1f2b");

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            var closedAt = loan.ClosedAt ?? r.TransactionDate;
                            c.Item().Text(t => { t.Span("முடிவு தேதி: ").SemiBold().FontSize(11); t.Span(closedAt.ToString("dd-MM-yyyy")).FontSize(11); });
                            c.Item().PaddingTop(6).Text(t => { t.Span("முடிவு நேரம்: ").SemiBold().FontSize(11); t.Span(closedAt.ToString("hh:mm tt")).FontSize(11); });
                        });

                        row.ConstantItem(30); // spacer

                        row.ConstantItem(130).Element(e =>
                            RenderPhotoBox(e, "முடிவின் போது வாடிக்கையாளர் புகைப்படம்", closurePhoto, width: 130, height: 130, labelFontSize: 9));
                    });

                    //col.Item().PaddingTop(24).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    //{
                    //    ack.Item().Text("அறிவிப்பு:").Bold().FontSize(7.5f);
                    //    ack.Item().Text("இந்த கணக்கு முழுவதுமாக முடிக்கப்பட்டதன் மூலம், அடகு வைக்கப்பட்ட தங்க நகைகள்/பொருட்கள் அனைத்தையும் பாதுகாப்பாக, சேதமின்றி, மூல நிலையில் பெற்றுக்கொண்டதாக இதன் மூலம் உறுதிப்படுத்துகிறேன்.").Italic().FontSize(7.5f);
                    //});
                    col.Item().PaddingTop(24).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    {
                        //ack.Item().Text("1. ஒவ்வொரு நகையும் 1 வருடத் தவணைக்குள் மீட்கப்பட வேண்டும். மீட்க தவறினால் அடகு மீட்க ஒப்புக்கொண்ட கால அளவுக்கு மேல் 7 நாட்களுக்குள் மேற்படி மீட்பு அங்கீகரிக்கப்படும். மேலும் தவறினால் அடகு வைத்தவருக்கு நோட்டீஸ் கொடுத்துவிட்டு அதன் பிறகு பகிரங்க ஏலத்தில் நகைகள் ஏலம் போடப்படும்.").FontSize(7.5f);
                        //ack.Item().PaddingTop(4).Text("2. வீடு மாறினாலோ அல்லது ரசீது தவறினாலோ உடனடியாக தெரிவிக்க வேண்டும். தவறினால் நாங்கள் ஜவாப்தாரியல்ல.").FontSize(7.5f);
                        //ack.Item().PaddingTop(4).Text("3. அடகு பொருட்களின் விலை மதிப்பு குறைந்தால் (Depreciation of Price) அடகு வைத்தவர் உடனே மார்ஜின் தொகை கட்ட வேண்டும். தவறினால் அடகு வைத்தவருக்கு தெரியப்படுத்தி அடகு பொருட்கள் பகிரங்க ஏலம் போடப்படும்.").FontSize(7.5f);
                        //ack.Item().PaddingTop(4).Text("4. இதில் கண்ட அசல் வட்டி தொகைகளை செலுத்தி முன் பக்கத்தில் கண்ட அடகு பொருட்களை சரிபார்த்து பெற்று கொண்டேன் .").FontSize(7.5f);
                        ack.Item().Row(row =>
                        {
                            row.ConstantItem(18)
                                .Text("1.")
                                .FontSize(7.5f);

                            row.RelativeItem()
                                .Text("ஒவ்வொரு நகையும் 1 வருடத் தவணைக்குள் மீட்கப்பட வேண்டும். மீட்க தவறினால் அடகு மீட்க ஒப்புக்கொண்ட கால அளவுக்கு மேல் 7 நாட்களுக்குள் மேற்படி மீட்பு அங்கீகரிக்கப்படும். மேலும் தவறினால் அடகு வைத்தவருக்கு நோட்டீஸ் கொடுத்துவிட்டு அதன் பிறகு பகிரங்க ஏலத்தில் நகைகள் ஏலம் போடப்படும்.")
                                .FontSize(7.5f)
                                .Justify();
                        });

                        ack.Item().PaddingTop(5).Row(row =>
                        {
                            row.ConstantItem(18)
                                .Text("2.")
                                .FontSize(7.5f);

                            row.RelativeItem()
                                .Text("வீடு மாறினாலோ அல்லது ரசீது தவறினாலோ உடனடியாக தெரிவிக்க வேண்டும். தவறினால் நாங்கள் ஜவாப்தாரியல்ல.")
                                .FontSize(7.5f)
                                .Justify();
                        });

                        ack.Item().PaddingTop(5).Row(row =>
                        {
                            row.ConstantItem(18)
                                .Text("3.")
                                .FontSize(7.5f);

                            row.RelativeItem()
                                .Text("அடகு பொருட்களின் விலை மதிப்பு குறைந்தால் (Depreciation of Price) அடகு வைத்தவர் உடனே மார்ஜின் தொகை கட்ட வேண்டும். தவறினால் அடகு வைத்தவருக்கு தெரியப்படுத்தி அடகு பொருட்கள் பகிரங்க ஏலம் போடப்படும்.")
                                .FontSize(7.5f)
                                .Justify();
                        });

                        ack.Item().PaddingTop(5).Row(row =>
                        {
                            row.ConstantItem(18)
                                .Text("4.")
                                .FontSize(7.5f);

                            row.RelativeItem()
                                .Text("இதில் கண்ட அசல் வட்டி தொகைகளை செலுத்தி முன் பக்கத்தில் கண்ட அடகு பொருட்களை சரிபார்த்து பெற்று கொண்டேன்.")
                                .FontSize(7.5f)
                                .Justify();
                        });


                    });
                });

                page.Footer().Column(foot =>
                {
                    RenderSignatureRow(foot, paddingTop: 20, rightLabel: "அங்கீகரிக்கப்பட்ட கையொப்பமிடுபவர் - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");

                    foot.Item().PaddingTop(10).Text(
                        "உங்கள் வணிகத்திற்கு நன்றி. இந்த கணக்கு முழுவதுமாக தீர்க்கப்பட்டு முடிக்கப்பட்டுள்ளது. " +
                        "இந்த எண்ணின் கீழ் மேலும் நிலுவைத் தொகை எதுவும் இல்லை.")
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
