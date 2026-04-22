using System.Globalization;
using System.Text;
using CabinetDesigner.Application.Export;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using Xunit;

namespace CabinetDesigner.Tests.Export;

public sealed class CutListExporterTests
{
    private static readonly ProjectSummary Summary = new(
        "Shop Floor Demo",
        "Rev A",
        DateTimeOffset.Parse("2026-04-21T16:00:00Z", CultureInfo.InvariantCulture),
        "Carpenter Studio");

    [Fact]
    public void Export_WithSampleManufacturingPlan_ProducesNonEmptyCsvTxtHtml()
    {
        var exporter = new CutListExporter(new CatalogService());
        var request = CreateSampleRequest();

        var result = exporter.Export(request);

        Assert.NotEmpty(result.Csv);
        Assert.NotEmpty(result.Txt);
        Assert.NotEmpty(result.Html);
        Assert.False(string.IsNullOrWhiteSpace(result.ContentHash));
    }

    [Fact]
    public void Export_IsDeterministic_AcrossRepeatedRuns()
    {
        var exporter = new CutListExporter(new CatalogService());
        var request = CreateSampleRequest();

        var first = exporter.Export(request);
        var second = exporter.Export(request);

        Assert.Equal(first.Csv, second.Csv);
        Assert.Equal(first.Txt, second.Txt);
        Assert.Equal(first.Html, second.Html);
        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void Export_CsvHeader_MatchesSpec()
    {
        var exporter = new CutListExporter(new CatalogService());

        var result = exporter.Export(CreateSampleRequest());
        var csvText = Encoding.UTF8.GetString(result.Csv);
        var header = csvText.Split("\r\n", StringSplitOptions.None)[0].TrimStart('\uFEFF');

        Assert.Equal("Cabinet,PartType,Label,Width(in),Height(in),Thickness(in),Material,GrainDirection,EdgeTop,EdgeBottom,EdgeLeft,EdgeRight,Qty", header);
    }

    [Fact]
    public void Export_CsvRowCount_MatchesPartCount()
    {
        var exporter = new CutListExporter(new CatalogService());
        var request = CreateSampleRequest();

        var result = exporter.Export(request);
        var csvText = Encoding.UTF8.GetString(result.Csv);
        var lines = csvText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(request.Plan.CutList.Count + 1, lines.Length);
    }

    [Fact]
    public void Export_TxtContainsOneSectionPerCabinet()
    {
        var exporter = new CutListExporter(new CatalogService());

        var result = exporter.Export(CreateSampleRequest());
        var txt = Encoding.UTF8.GetString(result.Txt);

        Assert.Contains("Cabinet: base-standard-24-1", txt, StringComparison.Ordinal);
        Assert.Contains("Cabinet: wall-standard-30", txt, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_HtmlIsSelfContained_NoExternalReferences()
    {
        var exporter = new CutListExporter(new CatalogService());

        var result = exporter.Export(CreateSampleRequest());
        var html = Encoding.UTF8.GetString(result.Html);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_ContentHashChanges_WhenAPartIsRemoved()
    {
        var exporter = new CutListExporter(new CatalogService());
        var full = CreateSampleRequest();
        var trimmed = full with
        {
            Plan = full.Plan with
            {
                CutList = full.Plan.CutList.Take(2).ToArray()
            },
            Materials = full.Materials.Take(2).ToArray()
        };

        var first = exporter.Export(full);
        var second = exporter.Export(trimmed);

        Assert.NotEqual(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void Export_HandlesCabinetWithCommaInName()
    {
        var exporter = new CutListExporter(new CatalogService());
        var request = CreateSampleRequest(cabinetPrefixOverride: "Base, Island");

        var result = exporter.Export(request);
        var csv = Encoding.UTF8.GetString(result.Csv);

        Assert.Contains("\"Base, Island\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_UsesInvariantCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var german = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;

            var exporter = new CutListExporter(new CatalogService());
            var result = exporter.Export(CreateSampleRequest());
            var csv = Encoding.UTF8.GetString(result.Csv);
            var txt = Encoding.UTF8.GetString(result.Txt);

            Assert.Contains("24.000", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("24,000", csv, StringComparison.Ordinal);
            Assert.Contains("24.000", txt, StringComparison.Ordinal);
            Assert.DoesNotContain("24,000", txt, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static CutListExportRequest CreateSampleRequest(string? cabinetPrefixOverride = null)
    {
        var catalog = new CatalogService();
        var baseCabinetId = new CabinetId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var wallCabinetId = new CabinetId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var baseMaterial = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless);
        var wallMaterial = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Wall, ConstructionMethod.Frameless);
        var threeQuarter = Thickness.Exact(Length.FromInches(0.75m));
        var cutList = new[]
        {
            CreateCutListItem(baseCabinetId, "LeftSide", cabinetPrefixOverride ?? "base-standard-24-1", 24m, 34.5m, baseMaterial, GrainDirection.LengthWise),
            CreateCutListItem(baseCabinetId, "RightSide", cabinetPrefixOverride ?? "base-standard-24-1", 24m, 34.5m, baseMaterial, GrainDirection.LengthWise),
            CreateCutListItem(wallCabinetId, "LeftSide", "wall-standard-30", 12m, 30m, wallMaterial, GrainDirection.LengthWise)
        };

        return new CutListExportRequest(
            new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = cutList,
                Operations = [],
                EdgeBandingRequirements = [],
                Readiness = new ManufacturingReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            },
            Summary,
            new[]
            {
                new MaterialAssignment(cutList[0].PartId, baseMaterial, threeQuarter, GrainDirection.LengthWise),
                new MaterialAssignment(cutList[1].PartId, baseMaterial, threeQuarter, GrainDirection.LengthWise),
                new MaterialAssignment(cutList[2].PartId, wallMaterial, threeQuarter, GrainDirection.LengthWise)
            });
    }

    private static CutListItem CreateCutListItem(
        CabinetId cabinetId,
        string partType,
        string cabinetPrefix,
        decimal widthInches,
        decimal heightInches,
        MaterialId materialId,
        GrainDirection grainDirection) =>
        new()
        {
            PartId = $"part:{cabinetId.Value:D}:{partType}",
            CabinetId = cabinetId,
            PartType = partType,
            Label = $"{cabinetPrefix}-{partType}",
            CutWidth = Length.FromInches(widthInches),
            CutHeight = Length.FromInches(heightInches),
            MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
            MaterialId = materialId,
            GrainDirection = grainDirection,
            EdgeTreatment = new ManufacturedEdgeTreatment("edge-top", null, null, "edge-right")
        };
}
