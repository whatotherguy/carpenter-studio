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

    [Fact]
    public void Constructor_FillerWithZeroWidthThrows()
    {
        Assert.Throws<ArgumentException>(() => new EndCondition(EndConditionType.Filler, Length.Zero));
    }

    [Fact]
    public void WithFiller_ZeroWidthThrows()
    {
        Assert.Throws<ArgumentException>(() => EndCondition.WithFiller(Length.Zero));
    }

    [Fact]
    public void WithFiller_ValidWidthConstructsSuccessfully()
    {
        var condition = EndCondition.WithFiller(Length.FromInches(1m));

        Assert.Equal(EndConditionType.Filler, condition.Type);
        Assert.Equal(Length.FromInches(1m), condition.FillerWidth);
    }

    [Fact]
    public void Constructor_ScribeWithZeroWidthThrows()
    {
        Assert.Throws<ArgumentException>(() => new EndCondition(EndConditionType.Scribe, Length.Zero));
    }

    [Fact]
    public void WithScribe_ZeroWidthThrows()
    {
        Assert.Throws<ArgumentException>(() => EndCondition.WithScribe(Length.Zero));
    }

    [Fact]
    public void WithScribe_ValidWidthConstructsSuccessfully()
    {
        var condition = EndCondition.WithScribe(Length.FromInches(1m));

        Assert.Equal(EndConditionType.Scribe, condition.Type);
        Assert.Equal(Length.FromInches(1m), condition.FillerWidth);
    }
}
