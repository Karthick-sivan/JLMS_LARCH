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
    private readonly byte[]? _pillaiyarSuzhiBytes;

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
        _pillaiyarSuzhiBytes = LoadPillaiyarSuzhiBytes();
    }

    private static byte[]? LoadLogoBytes()
    {
        var assetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var path = Path.Combine(assetsRoot, "Darkgreen" + ext);
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }
        return null;
    }
    private static byte[]? LoadPillaiyarSuzhiBytes()
    {
        var assetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var path = Path.Combine(assetsRoot, "PillaiyarSuzhi" + ext);
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
    /// Call as: col.Item().Element(c => RenderLetterhead(c, branchId, branchName, city, state, padding, fontSize));
    /// </summary>
    private void RenderLetterhead(IContainer container, int branchId, string? branchName = null, string? city = null, string? state = null, float padding = 10, float fontSize = 8)
    {
        container
            .Border(1).BorderColor(Colors.Grey.Darken1)
            .PaddingHorizontal(padding)
            .PaddingBottom(padding)
            .Column(outer =>
            {
                // ---- Corner line, flush to top border, tight left/right ----
                if (branchId == 1)
                {
                    outer.Item().PaddingTop(2).Row(top =>
                    {
                        top.RelativeItem().AlignLeft().Text("ஸ்ரீ மாசங்கருப்பர் துணை!!").FontSize(fontSize);
                        top.ConstantItem(16).AlignCenter().Height(12).Element(e =>
                        {
                            if (_pillaiyarSuzhiBytes != null)
                                e.Image(_pillaiyarSuzhiBytes).FitHeight();
                        });
                        top.RelativeItem().AlignRight().Text("ஸ்ரீ முத்துமாரியம்மன் துணை!!").FontSize(fontSize);
                    });
                }

                outer.Item().PaddingTop(4).Row(row =>
                {
                    row.ConstantItem(50).AlignMiddle().Element(e =>
                    {
                        if (_logoBytes != null)
                            e.Image(_logoBytes).FitArea();
                    });

                    row.RelativeItem().Column(head =>
                    {
                        // For BranchId 1, show full company details in Tamil; for others, show branch details in English
                        if (branchId == 1)
                        {
                            head.Item().AlignCenter().Text("ஸ்ரீ மீனாட்சி பேங்கர்ஸ்").FontSize(fontSize + 6).Bold().FontColor("#7a1f2b");
                            head.Item().AlignCenter().Text("அரசு அங்கீகாரம் பெற்றது | பதிவு எண். - ").FontSize(fontSize - 0.5f);
                            head.Item().AlignCenter().Text("மாங்குளம் மெயின் ரோடு,ராமராஜபுரம், மதுரை - 625122").FontSize(fontSize - 0.5f);
                            head.Item().AlignCenter().Text("தொலைபேசி எண் : 7550098326").FontSize(fontSize - 0.5f);
                        }
                        else
                        {
                            head.Item().AlignCenter().Text(branchName ?? "-").FontSize(fontSize + 6).Bold().FontColor("#7a1f2b");
                            head.Item().AlignCenter().Text($"{city ?? "-"}, {state ?? "-"}").FontSize(fontSize - 0.5f);
                        }
                    });

                    row.ConstantItem(50);
                });
            });
    }
    /// <summary>
    /// Renders the letterhead box PLUS a right-hand info column (loan number / amount / date),
    /// matching the "அடகு எண் / அடகு ரூபாய் / அடகு தேதி" layout shown in the sample receipt.
    /// Call as: col.Item().Element(c => RenderLetterheadWithInfo(c, branchId, branchName, city, state, loanNumber, bookNo, loanAmount, loanDate, maturityDate));
    /// </summary>
    private void RenderLetterheadWithInfo(IContainer container, int branchId, string? branchName = null, string? city = null, string? state = null, string? loanNumber = null, string? bookNo = null, decimal loanAmount = 0, DateTime? loanDate = null,
     DateTime? maturityDate = null, float padding = 12, float fontSize = 9)
    {
        container
            .Border(1).BorderColor(Colors.Grey.Darken1)
            .PaddingHorizontal(padding)
            .PaddingBottom(padding)
            .Column(outer =>
            {
                // ---- Corner line, flush to top border, tight left/right ----
                if (branchId == 1)
                {
                    outer.Item().PaddingTop(2).Row(top =>
                    {
                        top.RelativeItem().AlignLeft().Text("ஸ்ரீ மாசங்கருப்பர் துணை!!").FontSize(fontSize);
                        top.ConstantItem(18).AlignCenter().Height(14).Element(e =>
                        {
                            if (_pillaiyarSuzhiBytes != null)
                                e.Image(_pillaiyarSuzhiBytes).FitHeight();
                        });
                        top.RelativeItem().AlignRight().Text("ஸ்ரீ முத்துமாரியம்மன் துணை!!").FontSize(fontSize);
                    });
                }

                outer.Item().PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(60).AlignMiddle().Element(e =>
                    {
                        if (_logoBytes != null)
                            e.Image(_logoBytes).FitArea();
                    });

                    row.ConstantItem(8);

                    row.RelativeItem(3).Column(head =>
                    {
                        // For BranchId 1, show full company details in Tamil; for others, show branch details in English
                        if (branchId == 1)
                        {
                            head.Item().AlignCenter().Text("ஸ்ரீ மீனாட்சி பேங்கர்ஸ்").FontSize(fontSize + 13).Bold().FontColor("#7a1f2b");
                            head.Item().AlignCenter().Text("அரசு அங்கீகாரம் பெற்றது | பதிவு எண். - ").FontSize(fontSize).FontColor(Colors.Blue.Darken2);
                            head.Item().AlignCenter().Text("மாங்குளம்  மெயின் ரோடு, ராமராஜபுரம், மதுரை - 625122").FontSize(fontSize - 0.5f);
                            head.Item().AlignCenter().Text("தொலைபேசி எண் : 7550098326").FontSize(fontSize - 0.5f);
                        }
                        else
                        {
                            head.Item().AlignCenter().Text(branchName ?? "-").FontSize(fontSize + 13).Bold().FontColor("#7a1f2b");
                            head.Item().AlignCenter().Text($"{city ?? "-"}, {state ?? "-"}").FontSize(fontSize - 0.5f);
                        }
                    });

                    row.ConstantItem(10);

                    row.RelativeItem(1).AlignMiddle().Column(info =>
                    {

                        if (branchId == 1)
                        {
                            info.Item().Text(t =>
                            {
                                t.Span("அடகு எண்: ").SemiBold().FontSize(8.5f);
                                t.Span(bookNo ?? "-").FontSize(8.5f);
                            });
                        }
                        else
                        {
                            info.Item().Text(t =>
                            {
                                t.Span("Loan No: ").SemiBold().FontSize(8.5f);
                                t.Span(loanNumber ?? "-").FontSize(8.5f);
                            });
                            info.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("Book No: ").SemiBold().FontSize(8.5f);
                                t.Span(bookNo ?? "-").FontSize(8.5f);
                            });
                        }
                        info.Item().PaddingTop(3).Text(t =>
                        {
                            t.Span(branchId == 1 ? "அசல் தொகை: " : "Loan Amount: ").SemiBold().FontSize(8.5f);
                            t.Span($"ரூ. {loanAmount:N2}").FontSize(8.5f);
                        });
                        info.Item().PaddingTop(3).Text(t =>
                        {
                            t.Span(branchId == 1 ? "அடகு தேதி: " : "Loan Date: ").SemiBold().FontSize(8.5f);
                            t.Span(loanDate?.ToString("dd-MM-yyyy") ?? DateTime.Now.ToString("dd-MM-yyyy")).FontSize(8.5f);
                        });
                        info.Item().PaddingTop(3).Text(t =>
                        {
                            t.Span(branchId == 1 ? "மீட்கப்பட வேண்டிய தேதி: " : "Maturity Date: ").SemiBold().FontSize(8.5f);
                            t.Span(maturityDate?.ToString("dd-MM-yyyy") ?? "-").FontSize(8.5f);
                        });
                    });
                });
            });
    }
    /// <summary>
    /// Renders a labelled, bordered photo box (used for jewel photo / customer photo),
    /// matching the framed-photo look in the sample receipt.
    /// </summary>
    private static void RenderPhotoBox(IContainer container, string label, byte[]? photoBytes, int branchId = 1,
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
                        e.Text(branchId == 1 ? "புகைப்படம் இல்லை" : "No Photo").FontSize(labelFontSize - 0.5f).FontColor(Colors.Grey.Darken1);
                });
        });
    }
    private static string CustomerNameWithGuardian(string customerName, string? guardianName)
    {
        return string.IsNullOrWhiteSpace(guardianName)
            ? customerName
            : $"{customerName}   S/O. {guardianName}";
    }

    /// <summary>
    /// Extracts the BookNo from a LoanNumber.
    /// For BranchId = 2, returns the full LoanNumber.
    /// For other branches, extracts the last 5 digits (book number portion) and converts to integer (strips leading zeros).
    /// Examples:
    /// BR262700001 → 1
    /// BR262700010 → 10
    /// BR262700100 → 100
    /// BR262701000 → 1000
    /// BR262710000 → 10000
    /// </summary>
    public static string ExtractBookNo(string loanNumber, int branchId)
    {
        if (string.IsNullOrWhiteSpace(loanNumber))
            return string.Empty;

        if (branchId == 100)
            return loanNumber;

        // Extract last 5 digits (the book number portion)
        if (loanNumber.Length < 5)
            return string.Empty;

        var lastFiveDigits = loanNumber.Substring(loanNumber.Length - 5);
        // Convert to int to strip leading zeros, then back to string
        return int.Parse(lastFiveDigits).ToString();
    }
    /// <summary>
    /// Renders the two-column signature row (pledger left, authorised signatory right).
    /// rightLabel examples: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக", "அங்கீகரிக்கப்பட்ட கையொப்பமிடுபவர் - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்"
    /// </summary>
    private static void RenderSignatureRow(ColumnDescriptor col, int branchId = 1, string? branchName = null, float paddingTop = 24,
         string rightLabelLine1 = "அடகு பிடிப்பவரின் கையெழுத்து",
         string rightLabelLine2 = "ஸ்ரீ மீனாட்சி பேங்கர்ஸ்",
         float fontSize = 7.5f)
    {
        col.Item().PaddingTop(paddingTop).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(0.5f);
                c.Item().PaddingTop(2).Text(branchId == 1 ? "அடகு வைப்பவர் கையெழுத்து அல்லது இடது கை பெருவிரல் ரேகை" : "Pledger Signature or Left Thumb Impression").FontSize(fontSize);
            });
            row.ConstantItem(20);
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(0.5f);
                c.Item().PaddingTop(2).Text(branchId == 1 ? rightLabelLine1 : "Authorised Signatory").FontSize(fontSize);
                c.Item().Text(branchId == 1 ? rightLabelLine2 : (branchName ?? "-")).FontSize(fontSize);
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
    private static void RenderAuctionFooter(ColumnDescriptor col, int branchId = 1, float fontSize = 7f)
    {
        if (branchId == 1)
        {
            col.Item().PaddingTop(10).Text(
                "ஆபிஸ் நேரம் : காலை 7 மணி முதல் இரவு 8 மணி வரை அனைத்து நாட்களும் செயல்படும். இந்த ரசீதை நகையை போல் பாதுகாக்கவும் " +
                "6 மாதத்திற்கு ஒரு முறை ரசீதை புதுப்பித்துக்கொள்ளவும் . இல்லை எனில் 1 வருடம் 7 நாட்களுக்குள் திருப்ப தவறினால் அடகு பொருள் ஏலம் மூலம் விடப்படும்.")
                .FontSize(fontSize).FontColor(Colors.Red.Darken2);
        }
        else
        {
            col.Item().PaddingTop(10).Text(
                "Office Hours: 7:00 AM to 8:00 PM, all days. Keep this receipt like jewellery. " +
                "Update the receipt once every 6 months. If not redeemed within 1 year and 7 days, the pledged item will be auctioned.")
                .FontSize(fontSize).FontColor(Colors.Red.Darken2);
        }
    }
    // -------------------------------------------------------------------------
    // PDF generators
    // -------------------------------------------------------------------------

    public byte[] GenerateReceipt(Loan loan, string? branchName = null, string? city = null, string? state = null)
    {
        var customer = loan.Customer!;
        var customerPhoto = ReadPhotoBytes(customer.PhotoPath);
        var jewelPhoto = ReadPhotoBytes(loan.GroupPhotoPath);

        var document = Document.Create(container =>
        {
            // ================= PAGE 1 =================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(TamilFont));

                page.Content().Column(col =>
                {
                    // ---- Letterhead (with loan number / amount / date on the right) ----
                    col.Item().Element(c => RenderLetterheadWithInfo(c, loan.BranchId, branchName, city, state, loan.LoanNumber, loan.BookNo, loan.LoanAmount, loan.LoanDate, loan.MaturityDate));

                    // ---- Customer Details ----
                    col.Item().PaddingTop(12).Column(c =>
                    {
                        c.Item().Text(t => { t.Span(loan.BranchId == 1 ? "வாடிக்கையாளர் பெயர்: " : "Customer Name: ").SemiBold(); t.Span(customer.CustomerName); });

                        if (!string.IsNullOrWhiteSpace(customer.GuardianName))
                        {
                            c.Item().Text(t => { t.Span(loan.BranchId == 1 ? "பாதுகாவலர் பெயர்: " : "Guardian Name: ").SemiBold(); t.Span(customer.GuardianName); });
                        }

                        c.Item().Text(t =>
                        {
                            t.Span(loan.BranchId == 1 ? "முகவரி: " : "Address: ").SemiBold();
                            t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.Pincode }
                                .Where(s => !string.IsNullOrWhiteSpace(s))));
                        });
                        c.Item().Text(t => { t.Span(loan.BranchId == 1 ? "தொலைபேசி: " : "Mobile: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });
                    });

                    // ---- Item Photo + jewel item table + Customer Photo ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        row.ConstantItem(75).Element(e =>
                            RenderPhotoBox(e, loan.BranchId == 1 ? "பொருள் படம்" : "Item Photo", jewelPhoto, loan.BranchId, width: 68, height: 78, labelFontSize: 7));

                        row.RelativeItem().PaddingHorizontal(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.9f);
                                c.RelativeColumn(1.9f);
                                c.RelativeColumn(1.9f);
                                c.RelativeColumn(1.7f);
                                c.RelativeColumn(0.9f);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "பொருள்" : "Item");
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "எண்ணிக்கை" : "Quantity").FontSize(8);
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "வடிவம்" : "Model");
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "வகை" : "Variant");
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "எடை (கி)" : "Weight (g)");
                            });

                            foreach (var ji in loan.JewelItems)
                            {
                                var text = (ji.Varient ?? "-").Replace(" ", "\n");
                                table.Cell().Element(BodyCell).Text(ji.JewelType?.JewelTypeName ?? "-");
                                table.Cell().Element(BodyCell).Text(ji.Quantity.ToString());
                                table.Cell().Element(BodyCell).Text(ji.Model ?? "-");
                                table.Cell().Element(BodyCell).Text(text);
                                table.Cell().Element(BodyCell).Text(ji.GrossWeightGrams.ToString("0.000"));
                            }

                            static IContainer HeaderCell(IContainer c) =>
                                c.Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten2).Padding(4);
                            static IContainer BodyCell(IContainer c) =>
                                c.Border(1).BorderColor(Colors.Grey.Darken1).Padding(4);
                        });

                        row.ConstantItem(90).Element(e =>
                            RenderPhotoBox(e, loan.BranchId == 1 ? "புகைப்படம்" : "Photo", customerPhoto, loan.BranchId, width: 80, height: 95));
                    });
                });

                page.Footer().Column(foot =>
                {
                    //RenderSignatureRow(foot, paddingTop: 20, rightLabel: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக", fontSize: 8);
                    //RenderSignatureRow(foot, paddingTop: 16, rightLabel: "அடகு பிடிப்பவரின் கையெழுத்து - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");
                    RenderSignatureRow(foot, loan.BranchId, branchName, paddingTop: 16, fontSize: 8);
                    RenderAuctionFooter(foot, loan.BranchId, fontSize: 7.5f);
                });
            });

            // ================= PAGE 2 — closure-confirmation layout, blank/no data =================
            RenderClosureConfirmationPage(container, loan.BranchId, closedAt: null, closurePhoto: null, isClosure: false);
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
                    col.Item().Element(c => RenderLetterhead(c, r.BranchId, r.BranchName, r.City, r.State, padding: 10, fontSize: 8));

                    // ---- Customer + Receipt details (single column, left aligned) ----
                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(130); c.RelativeColumn(); });

                        //t.Cell().Text("வாடிக்கையாளர்:").SemiBold().FontSize(8.5f);
                        //t.Cell().Text(r.CustomerName).FontSize(8.5f);

                        t.Cell().Text(r.BranchId == 1 ? "வாடிக்கையாளர்:" : "Customer:").SemiBold().FontSize(8.5f);
                        t.Cell().Text(r.CustomerName).FontSize(8.5f);

                        if (!string.IsNullOrWhiteSpace(r.GuardianName))
                        {
                            t.Cell().Text(r.BranchId == 1 ? "பாதுகாவலர் பெயர்:" : "Guardian Name:").SemiBold().FontSize(8.5f);
                            t.Cell().Text(r.GuardianName).FontSize(8.5f);
                        }

                        t.Cell().Text(r.BranchId == 1 ? "தொலைபேசி:" : "Mobile:").SemiBold().FontSize(8.5f);
                        t.Cell().Text(r.Mobile ?? "-").FontSize(8.5f);

                        t.Cell().Text(r.BranchId == 1 ? "ரசீது எண்:" : "Receipt No:").SemiBold().FontSize(8.5f);
                        t.Cell().Text(r.ReceiptNumber).FontSize(8.5f);
                        if (r.BranchId == 1)
                        {
                            t.Cell().Text("அடகு எண்:").SemiBold().FontSize(8.5f);
                            t.Cell().Text(r.BookNo ?? "-").FontSize(8.5f);
                        }
                        else
                        {
                            t.Cell().Text("Loan No:").SemiBold().FontSize(8.5f);
                            t.Cell().Text(r.LoanNo).FontSize(8.5f);

                            t.Cell().Text("Book No:").SemiBold().FontSize(8.5f);
                            t.Cell().Text(r.BookNo ?? "-").FontSize(8.5f);
                        }

                        t.Cell().Text(r.BranchId == 1 ? "தேதி:" : "Date:").SemiBold().FontSize(8.5f);
                        t.Cell().Text(r.TransactionDate.ToString("dd-MM-yyyy HH:mm")).FontSize(8.5f);

                        if (r.MaturityDate.HasValue)
                        {
                            t.Cell().Text(r.BranchId == 1 ? "மீட்கப்பட வேண்டிய தேதி:" : "Maturity Date:").SemiBold().FontSize(8.5f);
                            t.Cell().Text(r.MaturityDate.Value.ToString("dd-MM-yyyy")).FontSize(8.5f);
                        }
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

                        AddRow(r.BranchId == 1 ? "செலுத்தப்பட்ட வட்டி" : "Interest Paid", $"ரூ. {r.InterestPaid:N2}");
                        AddRow(r.BranchId == 1 ? "செலுத்தப்பட்ட அசல்" : "Principal Paid", $"ரூ. {r.PrincipalPaid:N2}");
                        AddRow(r.BranchId == 1 ? "பெறப்பட்ட தொகை" : "Amount Received", $"ரூ. {r.AmountReceived:N2}", bold: true);
                        AddRow(r.BranchId == 1 ? "மீதமுள்ள வட்டி" : "Remaining Interest", $"ரூ. {r.RemainingInterest:N2}");
                        AddRow(r.BranchId == 1 ? "மீதமுள்ள அசல்" : "Remaining Principal", $"ரூ. {r.RemainingPrincipal:N2}");

                        var balanceAfter = r.RemainingInterest + r.RemainingPrincipal;
                        AddRow(r.BranchId == 1 ? "செலுத்திய பின் மீதி" : "Balance After Payment", $"ரூ. {balanceAfter:N2}", bold: true);
                    });
                });

                // ---- Signatures + footer pinned to page bottom via page.Footer() ----
                page.Footer().Column(foot =>
                {
                    //RenderSignatureRow(foot, paddingTop: 16, rightLabel: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக");
                    //RenderSignatureRow(foot, paddingTop: 16, rightLabel: "அடகு பிடிப்பவரின் கையெழுத்து - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");
                    RenderSignatureRow(foot, r.BranchId, r.BranchName, paddingTop: 16);

                    RenderAuctionFooter(foot, r.BranchId);
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
                    col.Item().Element(c => RenderLetterhead(c, r.BranchId, r.BranchName, r.City, r.State, padding: 10, fontSize: 8));

                    // ---- Customer (left) + Receipt details (right) ----
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        // Customer block — left half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(58); c.RelativeColumn(); });
                            t.Cell().Text(r.BranchId == 1 ? "வாடிக்கையாளர்:" : "Customer:").SemiBold();
                            t.Cell().Text(r.CustomerName);

                            if (!string.IsNullOrWhiteSpace(r.GuardianName))
                            {
                                t.Cell().Text(r.BranchId == 1 ? "பாதுகாவலர் பெயர்:" : "Guardian Name:").SemiBold();
                                t.Cell().Text(r.GuardianName);
                            }

                            t.Cell().Text(r.BranchId == 1 ? "தொலைபேசி:" : "Mobile:").SemiBold();
                            t.Cell().Text(r.Mobile ?? "-");
                        });

                        // Receipt details — right half
                        row.RelativeItem().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(68); c.RelativeColumn(); });
                            //t.Cell().Text("ரசீது எண்:").SemiBold();
                            //t.Cell().Text(r.ReceiptNumber);
                            if (r.BranchId == 1)
                            {
                                t.Cell().Text("எண்:").SemiBold();
                                t.Cell().Text(r.BookNo ?? "-");
                            }
                            else
                            {
                                t.Cell().Text("No:").SemiBold();
                                t.Cell().Text(r.LoanNo);
                                t.Cell().Text("Book No:").SemiBold();
                                t.Cell().Text(r.BookNo ?? "-");
                            }
                            //t.Cell().Text("திட்டம்:").SemiBold();
                            //t.Cell().Text(r.LoanScheme ?? "-");
                            t.Cell().Text(r.BranchId == 1 ? "முடிவு தேதி:" : "Closure Date:").SemiBold();
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

                        AddRow(r.BranchId == 1 ? "நிலுவை அசல்" : "Outstanding Principal", $"ரூ. {r.OutstandingPrincipal:N2}");
                        AddRow(r.BranchId == 1 ? "நிலுவை வட்டி" : "Outstanding Interest", $"ரூ. {r.OutstandingInterest:N2}");
                        AddRow(r.BranchId == 1 ? "பிற கட்டணங்கள்" : "Other Charges", $"ரூ. {r.OtherCharges:N2}");
                        AddRow(r.BranchId == 1 ? "மொத்தமாக செலுத்தப்பட்ட தொகை" : "Total Amount Paid", $"ரூ. {r.GrandTotal:N2}", bold: true);
                        AddRow(r.BranchId == 1 ? "நிலை" : "Status", r.BranchId == 1 ? "முடிக்கப்பட்டது" : "Closed");
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
                        if (r.BranchId == 1)
                        {
                            ack.Item().Text("1. ஒவ்வொரு நகையும் 1 வருடத் தவணைக்குள் மீட்கப்பட வேண்டும். மீட்க தவறினால் அடகு மீட்க ஒப்புக்கொண்ட கால அளவுக்கு மேல் 7 நாட்களுக்குள் மேற்படி மீட்பு அங்கீகரிக்கப்படும். மேலும் தவறினால் அடகு வைத்தவருக்கு நோட்டீஸ்  கொடுத்துவிட்டு அதன் பிறகு பகிரங்க ஏலத்தில் நகைகள் ஏலம் போடப்படும்.").FontSize(7.5f);
                            ack.Item().PaddingTop(4).Text("2. வீடு மாறினாலோ அல்லது ரசீது தவறினாலோ உடனடியாக தெரிவிக்க வேண்டும். தவறினால் நாங்கள்  ஜவாப்தாரியல்ல.").FontSize(7.5f);
                            ack.Item().PaddingTop(4).Text("3. அடகு பொருட்களின் விலை மதிப்பு குறைந்தால் (Depreciation of Price) அடகு வைத்தவர் உடனே மார்ஜின் தொகை கட்ட வேண்டும். தவறினால் அடகு வைத்தவருக்கு தெரியப்படுத்தி அடகு பொருட்கள் பகிரங்க ஏலம் போடப்படும்.").FontSize(7.5f);
                            ack.Item().PaddingTop(4).Text("4. இதில் கண்ட அசல் வட்டி தொகைகளை செலுத்தி முன் பக்கத்தில் கண்ட அடகு பொருட்களை சரிபார்த்து பெற்று கொண்டேன் .").FontSize(7.5f);
                        }
                        else
                        {
                            ack.Item().Text("1. Each jewellery must be redeemed within 1 year. If not redeemed, the redemption will be approved within 7 days beyond the agreed period. If not, notice will be given to the pledger and the jewellery will be auctioned publicly.").FontSize(7.5f);
                            ack.Item().PaddingTop(4).Text("2. If house changes or receipt is lost, inform immediately. If not, we are not responsible.").FontSize(7.5f);
                            ack.Item().PaddingTop(4).Text("3. If the value of pledged items decreases (Depreciation of Price), the pledger must pay the margin amount immediately. If not, the pledger will be informed and the pledged items will be auctioned publicly.").FontSize(7.5f);
                            ack.Item().PaddingTop(4).Text("4. I have received the pledged items shown on the previous page after paying the principal and interest amounts shown herein.").FontSize(7.5f);
                        }
                    });
                    // ---- Signatures ----
                    //RenderSignatureRow(foot, paddingTop: 50, rightLabel: "அங்கீகரிக்கப்பட்ட கையொப்பமிடுபவர் - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");
                    //RenderSignatureRow(foot, paddingTop: 50, rightLabel: "அடகு பிடிப்பவரின் கையெழுத்து - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்");
                    RenderSignatureRow(foot, r.BranchId, r.BranchName, paddingTop: 50);
                    // ---- Closure footer ----
                    foot.Item().PaddingTop(10).Text(
                    r.BranchId == 1 ? "உங்கள் வணிகத்திற்கு நன்றி. இந்த கணக்கு முழுவதுமாக தீர்க்கப்பட்டு முடிக்கப்பட்டுள்ளது. " +
                    "இந்த எண்ணின் கீழ் மேலும் நிலுவைத் தொகை எதுவும் இல்லை." : "Thank you for your business. This account has been fully settled and closed. " +
                    "There is no outstanding balance under this number.")
                    .FontSize(8f).FontColor(Colors.Green.Darken3).Bold();
                });

            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateClosureReceiptWithDetails(Loan loan, ClosureReceiptPdfDto r, string? branchName = null, string? city = null, string? state = null)
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
                    col.Item().Element(c => RenderLetterheadWithInfo(c, loan.BranchId, branchName, city, state, loan.LoanNumber, loan.BookNo, loan.LoanAmount, loan.LoanDate, loan.MaturityDate));

                    // ---- Customer Details (full width text block) ----
                    col.Item().PaddingTop(12).Column(c =>
                    {
                        //c.Item().Text(t => { t.Span("வாடிக்கையாளர் பெயர்: ").SemiBold(); t.Span(customer.CustomerName); });
                        c.Item().Text(t => { t.Span(loan.BranchId == 1 ? "வாடிக்கையாளர் பெயர்: " : "Customer Name: ").SemiBold(); t.Span(customer.CustomerName); });

                        if (!string.IsNullOrWhiteSpace(customer.GuardianName))
                        {
                            c.Item().Text(t => { t.Span(loan.BranchId == 1 ? "பாதுகாவலர் பெயர்: " : "Guardian Name: ").SemiBold(); t.Span(customer.GuardianName); });
                        }
                        c.Item().Text(t =>
                        {
                            t.Span(loan.BranchId == 1 ? "முகவரி: " : "Address: ").SemiBold();
                            t.Span(string.Join(", ", new[] { customer.Address, customer.City, customer.Pincode }
                                .Where(s => !string.IsNullOrWhiteSpace(s))));
                        });
                        c.Item().Text(t => { t.Span(loan.BranchId == 1 ? "தொலைபேசி: " : "Mobile: ").SemiBold(); t.Span(customer.Mobile ?? "-"); });
                        //c.Item().Text(t => { t.Span("அடகு திட்டம்: ").SemiBold(); t.Span(loan.LoanScheme?.SchemeName ?? "-"); });
                        // NOTE: அடகு எண் / அசல் தொகை / தேதி are shown once, in the letterhead box above — not repeated here.
                    });

                    // ---- Item Photo + jewel item table + Customer Photo (2nd box, right side) ----
                    col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                    {
                        // Jewel photo (left) — kept small so the table gets more room
                        row.ConstantItem(75).Element(e =>
                            RenderPhotoBox(e, loan.BranchId == 1 ? "பொருள் படம்" : "Item Photo", jewelPhoto, loan.BranchId, width: 68, height: 78, labelFontSize: 7));

                        // Jewel item table (middle)
                        row.RelativeItem().PaddingHorizontal(10).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.9f);
                                c.RelativeColumn(1.9f);   // wider so "எண்ணிக்கை" fits on one line
                                c.RelativeColumn(1.9f);
                                c.RelativeColumn(1.7f);
                                c.RelativeColumn(0.9f);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "பொருள்" : "Item");
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "எண்ணிக்கை" : "Quantity").FontSize(8);
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "வடிவம்" : "Model");
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "வகை" : "Variant");
                                h.Cell().Element(HeaderCell).Text(loan.BranchId == 1 ? "எடை (கி)" : "Weight (g)");
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
                            RenderPhotoBox(e, loan.BranchId == 1 ? " புகைப்படம்" : "Photo", customerPhoto, loan.BranchId, width: 80, height: 95));
                    });
                });

                page.Footer().Column(foot =>
                {
                    //RenderSignatureRow(foot, paddingTop: 20, rightLabel: "ஸ்ரீ மீனாட்சி பேங்கர்ஸ் சார்பாக", fontSize: 8);
                    //RenderSignatureRow(foot, paddingTop: 20, rightLabel: "அடகு பிடிப்பவரின் கையெழுத்து - ஸ்ரீ மீனாட்சி பேங்கர்ஸ்", fontSize: 8);
                    RenderSignatureRow(foot, loan.BranchId, branchName, paddingTop: 20, fontSize: 8);
                    RenderAuctionFooter(foot, loan.BranchId, fontSize: 7.5f);
                });
            });

            // ================= PAGE 2 — Closure confirmation + photo =================
            RenderClosureConfirmationPage(container, loan.BranchId, loan.ClosedAt ?? r.TransactionDate, closurePhoto, isClosure: true);
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

    private void RenderClosureConfirmationPage(IDocumentContainer container, int branchId, DateTime? closedAt, byte[]? closurePhoto, bool isClosure = false)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily(TamilFont));

            page.Content().Column(col =>
            {
                if (isClosure)
                {
                    col.Item().AlignCenter().PaddingTop(10)
                        .Text(branchId == 1 ? "கணக்கு  முடிவு உறுதிப்படுத்தல்" : "Account Closure Confirmation").FontSize(16).Bold().FontColor("#7a1f2b");
                }

                // ---- Repayment tracking grid ----
                // 4 columns per block (தேதி / வட்டி வரவு மாதம் / அசல் / கை எழுத்து) x 2 blocks = 8 columns total
                col.Item().PaddingTop(16).Border(1).BorderColor(Colors.Grey.Darken1).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(1.3f);
                        c.RelativeColumn(1.0f);
                        c.RelativeColumn(1.3f);
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(1.3f);
                        c.RelativeColumn(1.0f);
                        c.RelativeColumn(1.3f);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "தேதி" : "Date").FontSize(7.5f);
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "வட்டி\nவரவு\nமாதம்" : "Interest\nPaid\nMonth").FontSize(7.5f);
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "அசல்" : "Principal").FontSize(7.5f);
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "கை\nஎழுத்து" : "Sign\ature").FontSize(7.5f);
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "தேதி" : "Date").FontSize(7.5f);
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "வட்டி\nவரவு\nமாதம்" : "Interest\nPaid\nMonth").FontSize(7.5f);
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "அசல்" : "Principal").FontSize(7.5f);
                        h.Cell().Element(HeaderCell).Text(branchId == 1 ? "கை\nஎழுத்து" : "Sign\ature").FontSize(7.5f);
                    });

                    for (int i = 0; i < 8; i++)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            // j == 3 is "கை எழுத்து" (end of the first 4-col block) and MUST keep
                            // its right border so the vertical divider between it and the next
                            // "தேதி" column (j == 4) still renders. Only j == 7, the true outer
                            // last column, skips the right border.
                            bool isLastCol = (j == 7);
                            table.Cell().Element(isLastCol ? LastBodyCell : BodyCell).Text(" ");
                        }
                    }

                    // Uniform full border on every header cell — guarantees vertical AND
                    // horizontal lines render the same way the body grid box does.
                    static IContainer HeaderCell(IContainer c) =>
                        c.Border(1).BorderColor(Colors.Grey.Darken1)
                            .Background(Colors.Grey.Lighten2).Padding(3).AlignCenter();

                    static IContainer BodyCell(IContainer c) =>
                        c.BorderRight(1).BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(20);
                    static IContainer LastBodyCell(IContainer c) =>
                        c.BorderBottom(1).BorderColor(Colors.Grey.Darken1).Height(20);
                });

                if (isClosure)
                {
                    col.Item().PaddingTop(16).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    {
                        ack.Item().Text(branchId == 1 ? "இதில் கண்ட அசல் வட்டி தொகைகளை செலுத்தி முன் பக்கத்தில் கண்ட அடகு பொருட்களை சரிபார்த்து பெற்று கொண்டேன்." : "I have received the pledged items shown on the previous page after paying the principal and interest amounts shown herein.")
                            .FontSize(7.5f).Justify();
                    });
                }
                else
                {
                    col.Item().PaddingTop(16).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ack =>
                    {
                        if (branchId == 1)
                        {
                            ack.Item().Row(row =>
                            {
                                row.ConstantItem(18).Text("1.").FontSize(7.5f);
                                row.RelativeItem()
                                    .Text("ஒவ்வொரு நகையும் 1 வருடத் தவணைக்குள் மீட்கப்பட வேண்டும். மீட்க தவறினால் அடகு மீட்க ஒப்புக்கொண்ட கால அளவுக்கு மேல் 7 நாட்களுக்குள் மேற்படி மீட்பு அங்கீகரிக்கப்படும். மேலும் தவறினால் அடகு வைத்தவருக்கு நோட்டீஸ் கொடுத்துவிட்டு அதன் பிறகு பகிரங்க ஏலத்தில் நகைகள் ஏலம் போடப்படும்.")
                                    .FontSize(7.5f).Justify();
                            });

                            ack.Item().PaddingTop(5).Row(row =>
                            {
                                row.ConstantItem(18).Text("2.").FontSize(7.5f);
                                row.RelativeItem()
                                    .Text("வீடு மாறினாலோ அல்லது ரசீது தவறினாலோ உடனடியாக தெரிவிக்க வேண்டும். தவறினால் நாங்கள் ஜவாப்தாரியல்ல.")
                                    .FontSize(7.5f).Justify();
                            });

                            ack.Item().PaddingTop(5).Row(row =>
                            {
                                row.ConstantItem(18).Text("3.").FontSize(7.5f);
                                row.RelativeItem()
                                    .Text("அடகு பொருட்களின் விலை மதிப்பு குறைந்தால் (Depreciation of Price) அடகு வைத்தவர் உடனே மார்ஜின் தொகை கட்ட வேண்டும். தவறினால் அடகு வைத்தவருக்கு தெரியப்படுத்தி அடகு பொருட்கள் பகிரங்க ஏலம் போடப்படும்.")
                                    .FontSize(7.5f).Justify();
                            });
                        }
                        else
                        {
                            ack.Item().Row(row =>
                            {
                                row.ConstantItem(18).Text("1.").FontSize(7.5f);
                                row.RelativeItem()
                                    .Text("Each jewellery must be redeemed within 1 year. If not redeemed, the redemption will be approved within 7 days beyond the agreed period. If not, notice will be given to the pledger and the jewellery will be auctioned publicly.")
                                    .FontSize(7.5f).Justify();
                            });

                            ack.Item().PaddingTop(5).Row(row =>
                            {
                                row.ConstantItem(18).Text("2.").FontSize(7.5f);
                                row.RelativeItem()
                                    .Text("If house changes or receipt is lost, inform immediately. If not, we are not responsible.")
                                    .FontSize(7.5f).Justify();
                            });

                            ack.Item().PaddingTop(5).Row(row =>
                            {
                                row.ConstantItem(18).Text("3.").FontSize(7.5f);
                                row.RelativeItem()
                                    .Text("If the value of pledged items decreases (Depreciation of Price), the pledger must pay the margin amount immediately. If not, the pledger will be informed and the pledged items will be auctioned publicly.")
                                    .FontSize(7.5f).Justify();
                            });
                        }
                    });
                }
            });

            page.Footer().Column(foot =>
            {
                foot.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem(2).AlignBottom().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().PaddingTop(2).Text(branchId == 1 ? "அடகு வைப்பவர் கையெழுத்து அல்லது இடது கை பெருவிரல் ரேகை" : "Pledger Signature or Left Thumb Impression").FontSize(7.5f);
                    });

                    row.RelativeItem(1); // gap between the two blocks

                    row.ConstantItem(130).Column(c =>
                    {
                        c.Item().Element(e =>
                            RenderPhotoBox(e, branchId == 1 ? "முடிவின் போது வாடிக்கையாளர் புகைப்படம்" : "Customer Photo at Closure", closurePhoto, branchId, width: 130, height: 130, labelFontSize: 9));

                        c.Item().PaddingTop(6).Text(t =>
                        {
                            t.Span(branchId == 1 ? "முடிவு தேதி: " : "Closure Date: ").SemiBold().FontSize(10);
                            t.Span(closedAt.HasValue ? closedAt.Value.ToString("dd-MM-yyyy") : "-").FontSize(10);
                        });

                        c.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span(branchId == 1 ? "முடிவு நேரம்: " : "Closure Time: ").SemiBold().FontSize(10);
                            t.Span(closedAt.HasValue ? closedAt.Value.ToString("hh:mm tt") : "-").FontSize(10);
                        });
                    });
                });
                foot.Item().PaddingTop(10).Text(
                    branchId == 1 ? "உங்கள் வணிகத்திற்கு நன்றி. இந்த கணக்கு முழுவதுமாக தீர்க்கப்பட்டு முடிக்கப்பட்டுள்ளது. " +
                    "இந்த எண்ணின் கீழ் மேலும் நிலுவைத் தொகை எதுவும் இல்லை." : "Thank you for your business. This account has been fully settled and closed. " +
                    "There is no outstanding balance under this number.")
                    .FontSize(8f).FontColor(Colors.Green.Darken3).Bold();
            });
        });
    }
}
