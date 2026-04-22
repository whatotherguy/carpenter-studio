using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Export;

public sealed class CutListExporter : ICutListExporter
{
    private const string CsvHeader = "Cabinet,PartType,Label,Width(in),Height(in),Thickness(in),Material,GrainDirection,EdgeTop,EdgeBottom,EdgeLeft,EdgeRight,Qty";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ICatalogService _catalogService;

    public CutListExporter(ICatalogService catalogService)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
    }

    public CutListExportResult Export(CutListExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = BuildRows(request);
        var csv = BuildCsv(rows);
        var txt = BuildTxt(rows, request.Summary);
        var html = BuildHtml(rows, request.Summary);
        var contentHash = ComputeContentHash(csv, txt, html);
        return new CutListExportResult(csv, txt, html, contentHash);
    }

    private IReadOnlyList<CutListRow> BuildRows(CutListExportRequest request)
    {
        var materialsByPartId = request.Materials
            .OrderBy(assignment => assignment.PartId, StringComparer.Ordinal)
            .GroupBy(assignment => assignment.PartId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        return request.Plan.CutList
            .Select(item =>
            {
                var materialAssignment = materialsByPartId.TryGetValue(item.PartId, out var assignment)
                    ? assignment
                    : null;
                var effectiveMaterialId = materialAssignment?.MaterialId ?? item.MaterialId;
                var effectiveGrainDirection = materialAssignment?.GrainDirection ?? item.GrainDirection;
                return new CutListRow(
                    CabinetDisplayName: ResolveCabinetDisplayName(item),
                    CabinetId: item.CabinetId,
                    PartType: item.PartType,
                    Label: item.Label,
                    WidthInches: item.CutWidth.Inches,
                    HeightInches: item.CutHeight.Inches,
                    ThicknessInches: item.MaterialThickness.Actual.Inches,
                    Material: _catalogService.GetMaterialDisplayName(effectiveMaterialId),
                    GrainDirection: effectiveGrainDirection.ToString(),
                    EdgeTop: item.EdgeTreatment.TopEdgeBandingId ?? string.Empty,
                    EdgeBottom: item.EdgeTreatment.BottomEdgeBandingId ?? string.Empty,
                    EdgeLeft: item.EdgeTreatment.LeftEdgeBandingId ?? string.Empty,
                    EdgeRight: item.EdgeTreatment.RightEdgeBandingId ?? string.Empty,
                    PartId: item.PartId);
            })
            .OrderBy(row => row.CabinetDisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.PartType, StringComparer.Ordinal)
            .ThenBy(row => row.Label, StringComparer.Ordinal)
            .ThenBy(row => row.PartId, StringComparer.Ordinal)
            .ToArray();
    }

    private static byte[] BuildCsv(IReadOnlyList<CutListRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(CsvHeader).Append("\r\n");

        foreach (var row in rows)
        {
            builder.Append(EscapeCsv(row.CabinetDisplayName)).Append(',')
                .Append(EscapeCsv(row.PartType)).Append(',')
                .Append(EscapeCsv(row.Label)).Append(',')
                .Append(FormatNumber(row.WidthInches)).Append(',')
                .Append(FormatNumber(row.HeightInches)).Append(',')
                .Append(FormatNumber(row.ThicknessInches)).Append(',')
                .Append(EscapeCsv(row.Material)).Append(',')
                .Append(EscapeCsv(row.GrainDirection)).Append(',')
                .Append(EscapeCsv(row.EdgeTop)).Append(',')
                .Append(EscapeCsv(row.EdgeBottom)).Append(',')
                .Append(EscapeCsv(row.EdgeLeft)).Append(',')
                .Append(EscapeCsv(row.EdgeRight)).Append(',')
                // V2: collapse duplicate rows into aggregated quantities once the shop wants summarized cut lists.
                .Append('1')
                .Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Utf8NoBom.GetBytes(builder.ToString());
        return [.. preamble, .. content];
    }

    private static byte[] BuildTxt(IReadOnlyList<CutListRow> rows, ProjectSummary summary)
    {
        var builder = new StringBuilder();
        builder.Append("Carpenter Studio Cut List\n")
            .Append("Project: ").Append(summary.ProjectName).Append('\n')
            .Append("Revision: ").Append(summary.RevisionLabel).Append('\n')
            .Append("Generated: ").Append(FormatTimestamp(summary.GeneratedAtUtc)).Append('\n')
            .Append("------------------------------------------------\n");

        foreach (var cabinetGroup in rows.GroupBy(row => row.CabinetDisplayName, StringComparer.Ordinal))
        {
            builder.Append("Cabinet: ").Append(cabinetGroup.Key).Append('\n');
            foreach (var row in cabinetGroup)
            {
                builder.Append("  - ")
                    .Append(row.Label)
                    .Append(": ")
                    .Append(FormatNumber(row.WidthInches))
                    .Append("\" x ")
                    .Append(FormatNumber(row.HeightInches))
                    .Append("\" x ")
                    .Append(FormatNumber(row.ThicknessInches))
                    .Append("\" ")
                    .Append(row.Material)
                    .Append(" [grain: ")
                    .Append(row.GrainDirection)
                    .Append("]\n");
            }
        }

        builder.Append("------------------------------------------------\n")
            .Append("Total parts: ")
            .Append(rows.Count);

        return Utf8NoBom.GetBytes(builder.ToString());
    }

    private static byte[] BuildHtml(IReadOnlyList<CutListRow> rows, ProjectSummary summary)
    {
        var title = $"Cut List — {summary.ProjectName} — {summary.RevisionLabel}";
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>")
            .AppendLine("<html lang=\"en\">")
            .AppendLine("<head>")
            .AppendLine("  <meta charset=\"utf-8\">")
            .Append("  <title>").Append(HtmlEncode(title)).AppendLine("</title>")
            .AppendLine("  <style>")
            .AppendLine("    :root { color-scheme: light; }")
            .AppendLine("    * { box-sizing: border-box; }")
            .AppendLine("    body { font-family: \"Segoe UI\", Arial, sans-serif; margin: 24px; color: #1f2933; }")
            .AppendLine("    header, footer { margin-bottom: 24px; }")
            .AppendLine("    h1, h2 { margin: 0 0 12px; }")
            .AppendLine("    .summary { display: grid; grid-template-columns: repeat(2, minmax(180px, 1fr)); gap: 8px 16px; margin-top: 12px; }")
            .AppendLine("    .summary div { padding: 8px 10px; border: 1px solid #d9e2ec; background: #f8fbff; }")
            .AppendLine("    section { margin-bottom: 24px; page-break-inside: avoid; }")
            .AppendLine("    table { width: 100%; border-collapse: collapse; margin-top: 12px; }")
            .AppendLine("    th, td { border: 1px solid #bcccdc; padding: 6px 8px; text-align: left; vertical-align: top; }")
            .AppendLine("    th { background: #eef2f6; }")
            .AppendLine("    td.num { text-align: right; font-variant-numeric: tabular-nums; }")
            .AppendLine("    @media print { body { margin: 0.5in; } a { color: inherit; text-decoration: none; } }")
            .AppendLine("  </style>")
            .AppendLine("</head>")
            .AppendLine("<body>")
            .AppendLine("  <header>")
            .Append("    <h1>").Append(HtmlEncode("Carpenter Studio Cut List")).AppendLine("</h1>")
            .AppendLine("    <div class=\"summary\">")
            .Append("      <div><strong>Project:</strong> ").Append(HtmlEncode(summary.ProjectName)).AppendLine("</div>")
            .Append("      <div><strong>Revision:</strong> ").Append(HtmlEncode(summary.RevisionLabel)).AppendLine("</div>")
            .Append("      <div><strong>Generated:</strong> ").Append(HtmlEncode(FormatTimestamp(summary.GeneratedAtUtc))).AppendLine("</div>")
            .Append("      <div><strong>Generated By:</strong> ").Append(HtmlEncode(summary.GeneratedBy)).AppendLine("</div>")
            .AppendLine("    </div>")
            .AppendLine("  </header>");

        foreach (var cabinetGroup in rows.GroupBy(row => row.CabinetDisplayName, StringComparer.Ordinal))
        {
            builder.AppendLine("  <section>")
                .Append("    <h2>").Append(HtmlEncode(cabinetGroup.Key)).AppendLine("</h2>")
                .AppendLine("    <table>")
                .AppendLine("      <thead>")
                .AppendLine("        <tr>")
                .AppendLine("          <th>Cabinet</th>")
                .AppendLine("          <th>PartType</th>")
                .AppendLine("          <th>Label</th>")
                .AppendLine("          <th>Width(in)</th>")
                .AppendLine("          <th>Height(in)</th>")
                .AppendLine("          <th>Thickness(in)</th>")
                .AppendLine("          <th>Material</th>")
                .AppendLine("          <th>GrainDirection</th>")
                .AppendLine("          <th>EdgeTop</th>")
                .AppendLine("          <th>EdgeBottom</th>")
                .AppendLine("          <th>EdgeLeft</th>")
                .AppendLine("          <th>EdgeRight</th>")
                .AppendLine("          <th>Qty</th>")
                .AppendLine("        </tr>")
                .AppendLine("      </thead>")
                .AppendLine("      <tbody>");

            foreach (var row in cabinetGroup)
            {
                builder.AppendLine("        <tr>")
                    .Append("          <td>").Append(HtmlEncode(row.CabinetDisplayName)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.PartType)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.Label)).AppendLine("</td>")
                    .Append("          <td class=\"num\">").Append(FormatNumber(row.WidthInches)).AppendLine("</td>")
                    .Append("          <td class=\"num\">").Append(FormatNumber(row.HeightInches)).AppendLine("</td>")
                    .Append("          <td class=\"num\">").Append(FormatNumber(row.ThicknessInches)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.Material)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.GrainDirection)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.EdgeTop)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.EdgeBottom)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.EdgeLeft)).AppendLine("</td>")
                    .Append("          <td>").Append(HtmlEncode(row.EdgeRight)).AppendLine("</td>")
                    .AppendLine("          <td class=\"num\">1</td>")
                    .AppendLine("        </tr>");
            }

            builder.AppendLine("      </tbody>")
                .AppendLine("    </table>")
                .AppendLine("  </section>");
        }

        builder.AppendLine("  <footer>")
            .Append("    <div><strong>Total parts:</strong> ").Append(rows.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("</div>")
            .Append("    <div><strong>Generated:</strong> ").Append(HtmlEncode(FormatTimestamp(summary.GeneratedAtUtc))).AppendLine("</div>")
            .AppendLine("  </footer>")
            .AppendLine("</body>")
            .AppendLine("</html>");

        return Utf8NoBom.GetBytes(builder.ToString());
    }

    private static string ComputeContentHash(byte[] csv, byte[] txt, byte[] html)
    {
        var combined = new byte[csv.Length + txt.Length + html.Length];
        Buffer.BlockCopy(csv, 0, combined, 0, csv.Length);
        Buffer.BlockCopy(txt, 0, combined, csv.Length, txt.Length);
        Buffer.BlockCopy(html, 0, combined, csv.Length + txt.Length, html.Length);
        return Convert.ToHexString(SHA256.HashData(combined)).ToLowerInvariant();
    }

    private static string ResolveCabinetDisplayName(CutListItem item)
    {
        var ordinalSuffix = "-" + item.PartType;
        if (item.Label.EndsWith(ordinalSuffix, StringComparison.Ordinal))
        {
            return item.Label[..^ordinalSuffix.Length];
        }

        var secondOrdinalSuffix = "-" + item.PartType + "-";
        var index = item.Label.LastIndexOf(secondOrdinalSuffix, StringComparison.Ordinal);
        if (index > 0)
        {
            return item.Label[..index];
        }

        return item.CabinetId.Value.ToString("D", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string FormatNumber(decimal value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private sealed record CutListRow(
        string CabinetDisplayName,
        CabinetId CabinetId,
        string PartType,
        string Label,
        decimal WidthInches,
        decimal HeightInches,
        decimal ThicknessInches,
        string Material,
        string GrainDirection,
        string EdgeTop,
        string EdgeBottom,
        string EdgeLeft,
        string EdgeRight,
        string PartId);
}
