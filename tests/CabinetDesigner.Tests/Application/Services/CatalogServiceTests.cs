using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.Services;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class CatalogServiceTests
{
    private readonly CatalogService _sut = new();

    [Fact]
    public void GetAllItems_Returns_Seven_Catalog_Items()
    {
        var items = _sut.GetAllItems();
        Assert.Equal(7, items.Count);
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
    [InlineData("base-24")]
    [InlineData("base-30")]
    [InlineData("base-36")]
    [InlineData("wall-30")]
    [InlineData("wall-36")]
    [InlineData("tall-24")]
    [InlineData("tall-36")]
    public void GetAllItems_Contains_Expected_TypeId(string typeId)
    {
        var items = _sut.GetAllItems();
        Assert.Contains(items, i => i.TypeId == typeId);
    }

    [Fact]
    public void StableIdFor_Returns_Known_Value_For_Base24()
    {
        // Hard-coded expected value derived from SHA-256("base-24")[0..15]
        // interpreted as a little-endian Guid. Changing the hash algorithm
        // will break this test, making any regression immediately visible.
        var expected = new Guid("a71bb0ff-2bc8-dccf-8ac0-9c8a8e43a488");
        var actual = CatalogService.StableIdFor("base-24");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void StableIdFor_Is_Deterministic()
    {
        Assert.Equal(
            CatalogService.StableIdFor("base-24"),
            CatalogService.StableIdFor("base-24"));
        Assert.Equal(
            CatalogService.StableIdFor("tall-36"),
            CatalogService.StableIdFor("tall-36"));
    }

    [Fact]
    public void StableIdFor_Different_Inputs_Produce_Different_Guids()
    {
        var slugs = new[] { "base-24", "base-30", "base-36", "wall-30", "wall-36", "tall-24", "tall-36" };
        var guids = slugs.Select(CatalogService.StableIdFor).ToList();
        Assert.Equal(guids.Count, guids.Distinct().Count());
    }

    [Fact]
    public void StableIdFor_Does_Not_Match_MD5_Derived_Guid()
    {
        // Guard against accidentally reverting to MD5.
        var input = "base-24";
        var md5Guid = new Guid(MD5.HashData(Encoding.UTF8.GetBytes(input)));
        Assert.NotEqual(md5Guid, CatalogService.StableIdFor(input));
    }
}

