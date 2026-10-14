using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class SnapshotPdfWriter : ISnapshotPdfWriter
{
    public SnapshotPdfWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Write(Snapshot snapshot)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(11));
                page.Content().Column(column =>
                {
                    var first = true;
                    foreach (var block in snapshot.Blocks)
                    {
                        if (!first)
                            column.Item().PageBreak();
                        first = false;
                        column.Item().Element(element => Draw(element, block));
                    }

                    if (snapshot.Blocks.Count == 0)
                        column.Item().Text(string.Empty);
                });
            });
        }).GeneratePdf();
    }

    private static void Draw(IContainer container, SnapshotBlock block)
    {
        if (block.Type is ComponentKinds.Title or ComponentKinds.Text)
        {
            container.Text(block.Text ?? "");
            return;
        }

        if (block.Type == ComponentKinds.Count)
        {
            container.Text((block.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
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
