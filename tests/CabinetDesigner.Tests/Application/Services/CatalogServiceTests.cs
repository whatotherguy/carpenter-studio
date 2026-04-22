using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.Services;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class CatalogServiceTests
{
    private readonly CatalogService _sut = new();

    [Fact]
    public void GetAllItems_Returns_Five_Catalog_Items()
    {
        var items = _sut.GetAllItems();
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public void GetAllItems_TypeIds_Are_Unique()
    {
        var items = _sut.GetAllItems();
        var typeIds = items.Select(i => i.TypeId).ToList();
        Assert.Equal(typeIds.Count, typeIds.Distinct().Count());
    }

    [Fact]
    public void GetAllItems_Is_Deterministic_Across_Multiple_Calls()
    {
        var first  = _sut.GetAllItems().Select(i => i.TypeId).ToList();
        var second = new CatalogService().GetAllItems().Select(i => i.TypeId).ToList();
        Assert.Equal(first, second);
    }

    // -----------------------------------------------------------------------
    // Verify the observable TypeId values returned by GetAllItems(): they are
    // deterministic across calls and match the well-known cabinet-type slugs
    // exposed by the catalog service.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("base-standard-24")]
    [InlineData("base-drawer-18")]
    [InlineData("wall-standard-30")]
    [InlineData("tall-pantry-24")]
    [InlineData("base-faceframe-24")]
    public void GetAllItems_Contains_Expected_TypeId(string typeId)
    {
        var items = _sut.GetAllItems();
        Assert.Contains(items, i => i.TypeId == typeId);
    }

    [Fact]
    public void StableIdFor_Is_Deterministic()
    {
        Assert.Equal(
            CatalogService.StableIdFor("base-standard-24"),
            CatalogService.StableIdFor("base-standard-24"));
        Assert.Equal(
            CatalogService.StableIdFor("tall-pantry-24"),
            CatalogService.StableIdFor("tall-pantry-24"));
    }

    [Fact]
    public void StableIdFor_Different_Inputs_Produce_Different_Guids()
    {
        var slugs = new[] { "base-standard-24", "base-drawer-18", "wall-standard-30", "tall-pantry-24", "base-faceframe-24" };
        var guids = slugs.Select(CatalogService.StableIdFor).ToList();
        Assert.Equal(guids.Count, guids.Distinct().Count());
    }

    [Fact]
    public void StableIdFor_Does_Not_Match_MD5_Derived_Guid()
    {
        // Guard against accidentally reverting to MD5.
        var input = "base-standard-24";
        var md5Guid = new Guid(MD5.HashData(Encoding.UTF8.GetBytes(input)));
        Assert.NotEqual(md5Guid, CatalogService.StableIdFor(input));
    }
}
