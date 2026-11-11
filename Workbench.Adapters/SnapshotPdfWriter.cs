using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class SnapshotPdfWriter : IReportPdf
{
    public const string BuiltInFamily = "Lato";

    private static string FontFamilyName = ResolveFontFamily(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "msjh.ttc"));

    public static string ResolveFontFamily(string fontFile)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        if (string.IsNullOrWhiteSpace(fontFile) || !File.Exists(fontFile))
            return UseBuiltInFamily();

        try
        {
            FontManager.RegisterFontFromFile(fontFile);
            var names = FontManager.GetRegisteredFonts().Select(font => font.FamilyName);
            var chosen = names.FirstOrDefault(name => name.Contains("JhengHei", StringComparison.OrdinalIgnoreCase))
                ?? names.FirstOrDefault(name => name.Contains("微軟正黑", StringComparison.Ordinal));
            return string.IsNullOrEmpty(chosen) || chosen == BuiltInFamily ? UseBuiltInFamily() : chosen;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return UseBuiltInFamily();
        }
    }

    private static string UseBuiltInFamily()
    {
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        return BuiltInFamily;
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
                    column.Spacing(8);
                    foreach (var block in report.Blocks)
                        column.Item().Element(element => Draw(element, block));

                    if (report.Blocks.Count == 0)
                        column.Item().Text(string.Empty);
                });
            });
        }).GeneratePdf();
    }

    private static void Draw(IContainer container, PrintBlock block)
    {
        if (block.Type == BlockKinds.Title)
        {
            container.Text(block.Text ?? "").FontSize(18);
            return;
        }

        if (block.Type is BlockKinds.Text or BlockKinds.Parameter)
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
