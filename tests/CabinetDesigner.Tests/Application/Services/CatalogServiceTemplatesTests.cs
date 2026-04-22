using System.Linq;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class CatalogServiceTemplatesTests
{
    private readonly CatalogService _sut = new();

    [Fact]
    public void Catalog_IncludesAllFiveV1Templates()
    {
        var items = _sut.GetAllItems();

        Assert.Equal(5, items.Count);
        Assert.Collection(
            items.OrderBy(item => item.TypeId),
            item => Assert.Equal("base-drawer-18", item.TypeId),
            item => Assert.Equal("base-faceframe-24", item.TypeId),
            item => Assert.Equal("base-standard-24", item.TypeId),
            item => Assert.Equal("tall-pantry-24", item.TypeId),
            item => Assert.Equal("wall-standard-30", item.TypeId));
    }

    [Fact]
    public void Template_base_standard_24_HasExpectedDimensions()
    {
        AssertTemplate("base-standard-24", CabinetCategory.Base, ConstructionMethod.Frameless, 24m, 24m, 34.5m, 1);
    }

    [Fact]
    public void Template_base_drawer_18_HasExpectedDimensions()
    {
        AssertTemplate("base-drawer-18", CabinetCategory.Base, ConstructionMethod.Frameless, 18m, 24m, 34.5m, 3);
    }

    [Fact]
    public void Template_wall_standard_30_HasExpectedDimensions()
    {
        AssertTemplate("wall-standard-30", CabinetCategory.Wall, ConstructionMethod.Frameless, 30m, 12m, 30m, 2);
    }

    [Fact]
    public void Template_tall_pantry_24_HasExpectedDimensions()
    {
        AssertTemplate("tall-pantry-24", CabinetCategory.Tall, ConstructionMethod.Frameless, 24m, 24m, 84m, 2);
    }

    [Fact]
    public void Template_base_faceframe_24_HasFaceFrameConstruction()
    {
        AssertTemplate("base-faceframe-24", CabinetCategory.Base, ConstructionMethod.FaceFrame, 24m, 24m, 34.5m, 1);
    }

    private void AssertTemplate(
        string typeId,
        CabinetCategory category,
        ConstructionMethod construction,
        decimal nominalWidthInches,
        decimal depthInches,
        decimal heightInches,
        int defaultOpenings)
    {
        var template = _sut.GetAllItems().Single(item => item.TypeId == typeId);

        Assert.Equal(category.ToString(), template.Category);
        Assert.Equal(construction, template.ConstructionMethod);
        Assert.Equal(nominalWidthInches, template.NominalWidth.Inches);
        Assert.Equal(depthInches, template.Depth.Inches);
        Assert.Equal(heightInches, template.Height.Inches);
        Assert.Equal(defaultOpenings, template.DefaultOpenings);
        Assert.False(string.IsNullOrWhiteSpace(template.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(template.Description));
    }
}
