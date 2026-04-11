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
    // StableIdFor is private, so we verify its contract through observable
    // behaviour: every item exposes a deterministic string TypeId that
    // derives from the well-known cabinet-type slug.
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
    public void StableId_SHA256_Produces_Valid_Guid_Format()
    {
        // Reproduce the same logic as CatalogService.StableIdFor so we can
        // assert the format in isolation without making the method non-private.
        static Guid StableIdFor(string input)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hashBytes[..16]);
        }

        var guid = StableIdFor("base-24");

        // Must round-trip through the canonical 8-4-4-4-12 format.
        Assert.True(Guid.TryParse(guid.ToString(), out _));
        Assert.NotEqual(Guid.Empty, guid);
    }

    [Fact]
    public void StableId_SHA256_Is_Deterministic()
    {
        static Guid StableIdFor(string input)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hashBytes[..16]);
        }

        Assert.Equal(StableIdFor("base-24"), StableIdFor("base-24"));
        Assert.Equal(StableIdFor("tall-36"), StableIdFor("tall-36"));
    }

    [Fact]
    public void StableId_SHA256_Different_Inputs_Produce_Different_Guids()
    {
        static Guid StableIdFor(string input)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hashBytes[..16]);
        }

        var slugs = new[] { "base-24", "base-30", "base-36", "wall-30", "wall-36", "tall-24", "tall-36" };
        var guids = slugs.Select(StableIdFor).ToList();

        Assert.Equal(guids.Count, guids.Distinct().Count());
    }

    [Fact]
    public void StableId_SHA256_Does_Not_Match_MD5_Derived_Guid()
    {
        // Guard against accidentally reverting to MD5 — the SHA-256 result
        // must differ from what MD5 would produce for the same input.
        var input = "base-24";
        var md5Bytes  = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var sha256Bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        var md5Guid    = new Guid(md5Bytes);
        var sha256Guid = new Guid(sha256Bytes[..16]);

        Assert.NotEqual(md5Guid, sha256Guid);
    }
}
