using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using Xunit;

namespace CabinetDesigner.Tests.Application.State;

public sealed class InMemoryDesignStateStoreTests
{
    [Fact]
    public void AddRun_CanBeRetrievedById()
    {
        var store = new InMemoryDesignStateStore();
        var run = CreateRun();

        store.AddRun(run, Point2D.Origin, new Point2D(96m, 0m));

        Assert.Same(run, store.GetRun(run.Id));
    }

    [Fact]
    public void GetRun_ReturnsNull_ForUnknownId()
    {
        var store = new InMemoryDesignStateStore();

        Assert.Null(store.GetRun(RunId.New()));
    }

    [Fact]
    public void RemoveEntity_CabinetRun_RemovesFromStore()
    {
        var store = new InMemoryDesignStateStore();
        var run = CreateRun();
        store.AddRun(run, Point2D.Origin, new Point2D(96m, 0m));

        store.RemoveEntity(run.Id.Value.ToString(), "CabinetRun");

        Assert.Null(store.GetRun(run.Id));
    }

    [Fact]
    public void RestoreEntity_RehydratesCabinetRun()
    {
        var store = new InMemoryDesignStateStore();
        var run = CreateRun();
        store.AddRun(run, Point2D.Origin, new Point2D(96m, 0m));
        var snapshot = store.CaptureRunValues(run);
        store.RemoveEntity(run.Id.Value.ToString(), "CabinetRun");

        store.RestoreEntity(run.Id.Value.ToString(), "CabinetRun", snapshot);

        Assert.NotNull(store.GetRun(run.Id));
        Assert.Equal(96m, store.GetRunSpatialInfo(run.Id)!.EndWorld.X);
    }

    private static CabinetRun CreateRun() =>
        new(RunId.New(), WallId.New(), Length.FromInches(96m));
}
