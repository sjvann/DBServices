using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class SnapshotPdfWriter : IReportPdf
{
    private static readonly string FontFamilyName = ResolveFont();

    private static string ResolveFont()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular = Path.Combine(fonts, "msjh.ttc");
        if (File.Exists(regular))
            FontManager.RegisterFontFromFile(regular);

        var names = FontManager.GetRegisteredFonts().Select(font => font.FamilyName).Distinct().ToArray();
        return names.FirstOrDefault(name => name.Contains("JhengHei", StringComparison.OrdinalIgnoreCase))
            ?? names.FirstOrDefault(name => name.Contains("微軟正黑", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("找不到正黑體。已註冊：" + string.Join("、", names.Take(12)));
    }

    public byte[] Write(PrintReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily(FontFamilyName));
                page.Content().Column(column =>
                {
                    var first = true;
                    foreach (var block in report.Blocks)
                    {
                        if (!first)
                            column.Item().PageBreak();
                        first = false;
                        column.Item().Element(element => Draw(element, block));
                    }

                    if (report.Blocks.Count == 0)
                        column.Item().Text(string.Empty);
                });
            });
        }).GeneratePdf();
    }

    private static void Draw(IContainer container, PrintBlock block)
    {
        if (block.Type is BlockKinds.Title or BlockKinds.Text or BlockKinds.Parameter)
        {
            container.Text(block.Text ?? "");
            return;
        }

        var columns = block.Columns.Count == 0 ? [" "] : block.Columns;
        container.Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                for (var index = 0; index < columns.Count; index++)
                    definition.RelativeColumn();
            });
            table.Header(header =>
            {
                foreach (var name in columns)
                    header.Cell().Text(name);
            });
            foreach (var row in block.Rows)
            {
                for (var index = 0; index < columns.Count; index++)
                {
                    var text = index < row.Count ? row[index] ?? "" : "";
                    table.Cell().Text(text);
                }
            }
        });
    }
}
