using CabinetDesigner.Persistence.Mapping;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Mapping;

public sealed class CabinetMapperRoundTripTests
{
    [Fact]
    public void CabinetMapper_RoundTripsExplicitValues()
    {
        var revisionId = RevisionId.New();
        var cabinet = new Cabinet(CabinetId.New(), revisionId, "base-36", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(36), Length.FromInches(24), Length.FromInches(34.5m));
        cabinet.SetOverride("scribe", new OverrideValue.OfLength(Length.FromInches(0.5m)));

        var row = CabinetMapper.ToRow(cabinet, revisionId, RunId.New(), 0, DateTimeOffset.UnixEpoch);
        var roundTrip = CabinetMapper.ToDomain(row);

        Assert.Equal(cabinet.Id, roundTrip.Id);
        Assert.Equal(cabinet.RevisionId, roundTrip.RevisionId);
        Assert.Equal(cabinet.CabinetTypeId, roundTrip.CabinetTypeId);
        Assert.Equal(cabinet.Category, roundTrip.Category);
        Assert.Equal(cabinet.Construction, roundTrip.Construction);
        Assert.Equal(cabinet.NominalWidth, roundTrip.NominalWidth);
        Assert.Equal(cabinet.Depth, roundTrip.Depth);
        Assert.Equal(cabinet.Height, roundTrip.Height);
        Assert.Equal(cabinet.Overrides, roundTrip.Overrides);
    }

    [Fact]
    public void CabinetMapper_SerializesOverridesDeterministically()
    {
        var revisionId = RevisionId.New();
        var first = new Cabinet(CabinetId.New(), revisionId, "base-36", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(36), Length.FromInches(24), Length.FromInches(34.5m));
        first.SetOverride("zeta", new OverrideValue.OfBool(true));
        first.SetOverride("alpha", new OverrideValue.OfLength(Length.FromInches(0.5m)));

        var second = new Cabinet(CabinetId.New(), revisionId, "base-36", CabinetCategory.Base, ConstructionMethod.Frameless, Length.FromInches(36), Length.FromInches(24), Length.FromInches(34.5m));
        second.SetOverride("alpha", new OverrideValue.OfLength(Length.FromInches(0.5m)));
        second.SetOverride("zeta", new OverrideValue.OfBool(true));

        var firstRow = CabinetMapper.ToRow(first, revisionId, RunId.New(), 0, DateTimeOffset.UnixEpoch);
        var secondRow = CabinetMapper.ToRow(second, revisionId, RunId.New(), 0, DateTimeOffset.UnixEpoch);

        Assert.Equal(firstRow.OverridesJson, secondRow.OverridesJson);
    }
}
