using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class SkeletonStageTests
{
    public static IEnumerable<object[]> SkeletonStages()
    {
        yield return [new CostingStage()];
        yield return [new ConstraintPropagationStage()];
        yield return [new PackagingStage()];
        yield return [new PartGenerationStage()];
        yield return [new EngineeringResolutionStage()];
    }

    [Theory]
    [MemberData(nameof(SkeletonStages))]
    public void SkeletonStage_Execute_ReturnsSuccessWithIsNotImplementedTrue(IResolutionStage stage)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand([]),
            Mode = ResolutionMode.Full
        };

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.True(result.IsNotImplemented);
    }

    [Theory]
    [MemberData(nameof(SkeletonStages))]
    public void SkeletonStage_Execute_DoesNotThrow(IResolutionStage stage)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand([]),
            Mode = ResolutionMode.Full
        };

        var ex = Record.Exception(() => stage.Execute(context));

        Assert.Null(ex);
    }

    [Fact]
    public void CostingStage_Execute_WithLogger_EmitsOneDebugLogEntry()
    {
        var logger = new RecordingAppLogger();
        var stage = new CostingStage(logger);
        var context = new ResolutionContext { Command = new TestDesignCommand([]), Mode = ResolutionMode.Full };

        stage.Execute(context);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
    }

    [Fact]
    public void ConstraintPropagationStage_Execute_WithLogger_EmitsOneDebugLogEntry()
    {
        var logger = new RecordingAppLogger();
        var stage = new ConstraintPropagationStage(logger);
        var context = new ResolutionContext { Command = new TestDesignCommand([]), Mode = ResolutionMode.Full };

        stage.Execute(context);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
    }

    [Fact]
    public void PackagingStage_Execute_WithLogger_EmitsOneDebugLogEntry()
    {
        var logger = new RecordingAppLogger();
        var stage = new PackagingStage(logger);
        var context = new ResolutionContext { Command = new TestDesignCommand([]), Mode = ResolutionMode.Full };

        stage.Execute(context);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
    }

    [Fact]
    public void PartGenerationStage_Execute_WithLogger_EmitsOneDebugLogEntry()
    {
        var logger = new RecordingAppLogger();
        var stage = new PartGenerationStage(logger);
        var context = new ResolutionContext { Command = new TestDesignCommand([]), Mode = ResolutionMode.Full };

        stage.Execute(context);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
    }

    [Fact]
    public void EngineeringResolutionStage_Execute_WithLogger_EmitsOneDebugLogEntry()
    {
        var logger = new RecordingAppLogger();
        var stage = new EngineeringResolutionStage(logger);
        var context = new ResolutionContext { Command = new TestDesignCommand([]), Mode = ResolutionMode.Full };

        stage.Execute(context);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
    }

    private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> Issues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "test", []);

        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => Issues;
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }
}
