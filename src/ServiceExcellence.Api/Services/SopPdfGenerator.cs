using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;

namespace ServiceExcellence.Api.Services;

/// <summary>Renders an SOP as a printable A4 PDF for the workshop floor.</summary>
public class SopPdfGenerator
{
    private const string Accent = "#1F4E79";

    private readonly IFileStorage _files;

    public SopPdfGenerator(IFileStorage files) => _files = files;

    public byte[] Generate(SopDetailDto sop)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9.5f));

                page.Header().Element(c => ComposeHeader(c, sop));
                page.Content().PaddingVertical(8).Element(c => ComposeContent(c, sop));
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{sop.SopCode} v{sop.Version} - {SopStatus.Display(sop.Status)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignCenter().Text($"Printed {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, SopDetailDto sop)
    {
        container.BorderBottom(1.5f).BorderColor(Accent).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("STANDARD OPERATING PROCEDURE").FontSize(8).Bold().FontColor(Accent).LetterSpacing(0.05f);
                col.Item().Text(sop.Title).FontSize(15).Bold();
                col.Item().Text($"{ActivityTypes.Display(sop.ActivityType)}  |  Applies to {sop.AppliesToLevel}: {sop.AppliesTo}")
                    .FontColor(Colors.Grey.Darken2);
            });
            row.ConstantItem(140).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(sop.SopCode).Bold().FontSize(11);
                col.Item().AlignRight().Text($"Version {sop.Version}");
                if (sop.Status != SopStatus.Published)
                    col.Item().AlignRight().Text($"NOT RELEASED - {SopStatus.Display(sop.Status).ToUpperInvariant()}").Bold().FontColor(Colors.Red.Darken2);
            });
        });
    }

    private void ComposeContent(IContainer container, SopDetailDto sop)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Classification and key facts
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(95);
                    c.RelativeColumn();
                    c.ConstantColumn(95);
                    c.RelativeColumn();
                });

                void Cell(string label, string? value)
                {
                    table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(label).Bold();
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(string.IsNullOrWhiteSpace(value) ? "All" : value);
                }

                Cell("Division", sop.DivisionName);
                Cell("Model category", sop.CategoryName);
                Cell("Model", sop.ModelName);
                Cell("Variant", sop.VariantName);
                Cell("Assembly", sop.AssemblyPath ?? sop.AssemblyName);
                Cell("Skill level", sop.SkillLevel ?? "-");
                Cell("Standard time", $"{sop.StandardTimeMinutes} min");
                Cell("Approved by", sop.ApprovedByName is null ? "-" : $"{sop.ApprovedByName} ({sop.ApprovedAt:dd-MMM-yyyy})");
            });

            if (!string.IsNullOrWhiteSpace(sop.Purpose))
            {
                col.Item().Element(SectionTitle).Text("Purpose & scope");
                col.Item().Text(sop.Purpose);
            }

            if (!string.IsNullOrWhiteSpace(sop.SafetyNotes))
            {
                col.Item().Background(Colors.Orange.Lighten5).Border(1).BorderColor(Colors.Orange.Medium).Padding(6).Column(c =>
                {
                    c.Item().Text("SAFETY PRECAUTIONS").Bold().FontColor(Colors.Orange.Darken3);
                    c.Item().Text(sop.SafetyNotes);
                });
            }

            if (sop.Resources.Count > 0)
            {
                col.Item().Element(SectionTitle).Text("Parts, tools & consumables");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.ConstantColumn(100);
                        c.RelativeColumn();
                        c.ConstantColumn(60);
                    });
                    table.Header(h =>
                    {
                        foreach (var title in new[] { "Type", "Part / tool no.", "Description", "Qty" })
                            h.Cell().Background(Accent).Padding(4).Text(title).Bold().FontColor(Colors.White);
                    });
                    foreach (var r in sop.Resources)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(r.ResourceType);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(r.PartNumber ?? "-");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(r.Description);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text($"{r.Quantity:0.##} {r.Uom}");
                    }
                });
            }

            col.Item().Element(SectionTitle).Text($"Procedure ({sop.Steps.Count} steps, {sop.TotalStepMinutes} min)");
            foreach (var step in sop.Steps)
                col.Item().Element(c => ComposeStep(c, step));

            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().PaddingTop(25).BorderTop(0.5f).Text("Technician name & signature").FontSize(8);
                });
                row.ConstantItem(30);
                row.RelativeItem().Column(c =>
                {
                    c.Item().PaddingTop(25).BorderTop(0.5f).Text("Supervisor / QC signature").FontSize(8);
                });
                row.ConstantItem(30);
                row.RelativeItem().Column(c =>
                {
                    c.Item().PaddingTop(25).BorderTop(0.5f).Text("Date / Job card no.").FontSize(8);
                });
            });
        });
    }

    private void ComposeStep(IContainer container, SopStepDto step)
    {
        container.EnsureSpace(90).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Row(row =>
        {
            row.ConstantItem(34).Background(Colors.Grey.Lighten3).AlignCenter().PaddingTop(6).Text(step.StepNo.ToString()).FontSize(14).Bold().FontColor(Accent);
            row.RelativeItem().Padding(6).Column(col =>
            {
                col.Spacing(3);
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text(step.Title).Bold().FontSize(10.5f);
                    if (step.EstimatedMinutes > 0)
                        r.ConstantItem(60).AlignRight().Text($"{step.EstimatedMinutes} min").FontColor(Colors.Grey.Darken1);
                });
                col.Item().Text(step.Instruction);
                if (!string.IsNullOrWhiteSpace(step.ToolsRequired))
                    col.Item().Text(t => { t.Span("Tools: ").Bold(); t.Span(step.ToolsRequired); });
                if (!string.IsNullOrWhiteSpace(step.Specification))
                    col.Item().Text(t => { t.Span("Specification: ").Bold(); t.Span(step.Specification); });
                if (!string.IsNullOrWhiteSpace(step.Caution))
                    col.Item().Text(t => { t.Span("CAUTION: ").Bold().FontColor(Colors.Red.Darken2); t.Span(step.Caution).FontColor(Colors.Red.Darken2); });

                foreach (var image in step.Attachments.Where(a => a.IsImage && a.ContentType != "image/webp" && a.ContentType != "image/gif"))
                {
                    using var stream = _files.OpenRead(image.StoredName);
                    if (stream == null) continue;
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    col.Item().PaddingTop(4).MaxHeight(180).MaxWidth(300).Image(buffer.ToArray()).FitArea();
                    col.Item().Text(image.FileName).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                }

                var others = step.Attachments.Where(a => !a.IsImage || a.ContentType is "image/webp" or "image/gif").ToList();
                if (others.Count > 0)
                    col.Item().Text("Attachments: " + string.Join(", ", others.Select(a => a.FileName))).FontSize(8).Italic();
            });
        });
    }

    private static IContainer SectionTitle(IContainer container) =>
        container.PaddingTop(4).BorderBottom(0.5f).BorderColor(Accent).PaddingBottom(2).DefaultTextStyle(t => t.FontSize(11).Bold().FontColor(Accent));
}
