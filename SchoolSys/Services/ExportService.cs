using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SchoolSys.Services;

/// <summary>عمود في تقرير مُصدَّر.</summary>
public record ExportColumn(string Header, float Width = 1f);

public interface IExportService
{
    byte[] ToExcel(string sheetTitle, IReadOnlyList<ExportColumn> columns, IEnumerable<string?[]> rows);

    byte[] ToPdf(string title, string? subtitle, IReadOnlyList<ExportColumn> columns,
        IEnumerable<string?[]> rows, string schoolName, string? footerNote = null,
        string? logoPath = null);
}

public class ExportService : IExportService
{
    private readonly string ArabicFont;
    private readonly string _webRoot;
    private readonly ILogger<ExportService> _logger;

    /// <summary>
    /// يسجّل خط Cairo المرفق مع المشروع في QuestPDF لتتطابق ملفات PDF
    /// مع الخط المعروض على الشاشة. عند تعذّر ذلك نعود إلى خط النظام:
    /// Arial على ويندوز و Noto Sans Arabic داخل حاوية لينكس.
    /// </summary>
    public ExportService(IWebHostEnvironment env, ILogger<ExportService> logger)
    {
        _logger = logger;
        _webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

        var explicitFont = Environment.GetEnvironmentVariable("PDF_FONT");
        var fallback = OperatingSystem.IsWindows() ? "Arial" : "Noto Sans Arabic";

        try
        {
            var webRoot = _webRoot;
            var path = Path.Combine(webRoot, "lib", "cairo", "pdf", "Cairo-Variable.ttf");

            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                QuestPDF.Drawing.FontManager.RegisterFont(stream);
                ArabicFont = "Cairo";
                logger.LogInformation("تم تسجيل خط Cairo لتوليد ملفات PDF.");
            }
            else
            {
                ArabicFont = explicitFont ?? fallback;
                logger.LogWarning("ملف خط Cairo غير موجود في {Path} — سيُستخدم {Font}.", path, ArabicFont);
            }
        }
        catch (Exception ex)
        {
            // تسجيل الخط ليس حرجاً — التقارير تعمل بخط النظام
            ArabicFont = explicitFont ?? fallback;
            logger.LogWarning(ex, "تعذّر تسجيل خط Cairo — سيُستخدم {Font}.", ArabicFont);
        }
    }

    public byte[] ToExcel(string sheetTitle, IReadOnlyList<ExportColumn> columns, IEnumerable<string?[]> rows)
    {
        using var wb = new XLWorkbook();
        var safeName = string.Join("", sheetTitle.Take(28).Where(c => !"[]:*?/\\".Contains(c)));
        var ws = wb.AddWorksheet(string.IsNullOrWhiteSpace(safeName) ? "تقرير" : safeName);
        ws.RightToLeft = true;

        // العنوان
        ws.Cell(1, 1).Value = sheetTitle;
        ws.Range(1, 1, 1, Math.Max(1, columns.Count)).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // رؤوس الأعمدة
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(3, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        var r = 4;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count && c < row.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                cell.SetValue(row[c] ?? string.Empty);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            r++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// يقرأ شعار المدرسة من مسار الويب المحفوظ في الإعدادات (مثل
    /// "/uploads/school/logo.png"). يعود بـ null بصمت عند أي تعذّر —
    /// فالتقرير يجب أن يخرج بشعار أو بدونه، لا أن يفشل.
    /// </summary>
    private byte[]? TryReadLogo(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath)) return null;

        try
        {
            var relative = logoPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(_webRoot, relative));

            // لا نقرأ خارج wwwroot مهما كان المسار المحفوظ
            var root = Path.GetFullPath(_webRoot);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;

            return File.Exists(full) ? File.ReadAllBytes(full) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذّرت قراءة شعار المدرسة من {Path} لإدراجه في PDF.", logoPath);
            return null;
        }
    }

    public byte[] ToPdf(string title, string? subtitle, IReadOnlyList<ExportColumn> columns,
        IEnumerable<string?[]> rows, string schoolName, string? footerNote = null,
        string? logoPath = null)
    {
        var data = rows.ToList();
        var logo = TryReadLogo(logoPath);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily(ArabicFont).FontSize(9));
                page.ContentFromRightToLeft();

                page.Header().Column(col =>
                {
                    // الشعار يمين الترويسة (الاتجاه من اليمين لليسار) والعناوين
                    // في الوسط، فتتطابق التقارير المصدَّرة مع مستندات النظام
                    col.Item().Row(row =>
                    {
                        if (logo is not null)
                            row.ConstantItem(48).Height(48).AlignMiddle().Image(logo).FitArea();
                        else
                            row.ConstantItem(48);

                        row.RelativeItem().AlignMiddle().Column(t =>
                        {
                            t.Item().Text(schoolName).FontSize(14).Bold()
                                .FontColor(Colors.Blue.Darken3).AlignCenter();
                            t.Item().Text(title).FontSize(12).SemiBold().AlignCenter();
                            if (!string.IsNullOrWhiteSpace(subtitle))
                                t.Item().Text(subtitle).FontSize(9)
                                    .FontColor(Colors.Grey.Darken1).AlignCenter();
                        });

                        // عمود مقابل بنفس عرض الشعار حتى تبقى العناوين في المنتصف
                        row.ConstantItem(48);
                    });

                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(def =>
                    {
                        foreach (var c in columns) def.RelativeColumn(c.Width);
                    });

                    table.Header(header =>
                    {
                        foreach (var c in columns)
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                .Text(c.Header).FontColor(Colors.White).SemiBold().AlignCenter();
                    });

                    var i = 0;
                    foreach (var row in data)
                    {
                        var bg = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                        for (var c = 0; c < columns.Count; c++)
                            table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(3)
                                .Text(c < row.Length ? row[c] ?? "" : "").AlignCenter();
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(footerNote ?? $"طُبع في {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignLeft().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
