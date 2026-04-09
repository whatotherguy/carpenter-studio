using System;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Identifiers;

public sealed class IdentifierTests
{
    [Fact]
    public void CommandId_New_ReturnsDifferentValuesOnEachCall()
    {
        var first = CommandId.New();
        var second = CommandId.New();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CommandId_New_ValueIsNotEmpty()
    {
        var id = CommandId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void CommandId_ToString_ReturnsGuidString()
    {
        var value = Guid.NewGuid();
        var id = new CommandId(value);

        Assert.Equal(value.ToString(), id.ToString());
    }

    [Fact]
    public void CabinetId_New_ReturnsDifferentValuesOnEachCall()
    {
        var first = CabinetId.New();
        var second = CabinetId.New();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RunId_New_ReturnsDifferentValuesOnEachCall()
    {
        var first = RunId.New();
        var second = RunId.New();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ExplanationNodeId_New_ReturnsDifferentValuesOnEachCall()
    {
        var first = ExplanationNodeId.New();
        var second = ExplanationNodeId.New();

        Assert.NotEqual(first, second);
    }
}
