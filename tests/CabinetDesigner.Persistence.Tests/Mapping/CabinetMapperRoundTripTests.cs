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
}
