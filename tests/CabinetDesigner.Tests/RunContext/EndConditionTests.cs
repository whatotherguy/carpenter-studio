using System;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.RunContext;
using Xunit;

namespace CabinetDesigner.Tests.RunContext;

public sealed class EndConditionTests
{
    [Fact]
    public void Open_FactoryCreatesOpenCondition()
    {
        var condition = EndCondition.Open();

        Assert.Equal(EndConditionType.Open, condition.Type);
        Assert.Null(condition.FillerWidth);
    }

    [Fact]
    public void AgainstWall_FactoryCreatesAgainstWallCondition()
    {
        var condition = EndCondition.AgainstWall();

        Assert.Equal(EndConditionType.AgainstWall, condition.Type);
    }

    [Fact]
    public void WithFiller_RequiresWidth()
    {
        var condition = EndCondition.WithFiller(Length.FromInches(2m));

        Assert.Equal(Length.FromInches(2m), condition.FillerWidth);
    }

    [Fact]
    public void Constructor_FillerWithoutWidthThrows()
    {
        Assert.Throws<ArgumentException>(() => new EndCondition(EndConditionType.Filler));
    }
}
