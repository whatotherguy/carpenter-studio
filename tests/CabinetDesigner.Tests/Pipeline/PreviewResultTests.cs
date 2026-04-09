using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class PreviewResultTests
{
    [Fact]
    public void PreviewResult_Succeeded_IncludesSpatialData()
    {
        var metadata = CreateMetadata();
        var spatial = new SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [],
            Placements = [new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(
                RunId.New(),
                CabinetId.New(),
                Point2D.Origin,
                new Vector2D(1m, 0m),
                new Rect2D(Point2D.Origin, Length.FromInches(1m), Length.FromInches(1m)),
                Length.FromInches(1m))]
        };

        var result = PreviewResult.Succeeded(metadata, spatial);

        Assert.True(result.Success);
        Assert.Same(spatial, result.SpatialResult);
    }

    [Fact]
    public void PreviewResult_Failed_HasNullSpatialData()
    {
        var metadata = CreateMetadata();

        var result = PreviewResult.Failed(
            metadata,
            [new ValidationIssue(ValidationSeverity.Error, "FAIL", "Failure")]);

        Assert.False(result.Success);
        Assert.Null(result.SpatialResult);
    }

    private static CommandMetadata CreateMetadata() =>
        CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Preview", []);
}
