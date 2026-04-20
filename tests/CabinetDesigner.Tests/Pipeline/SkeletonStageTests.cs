using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class SkeletonStageTests
{
    // All stages that previously returned NotImplementedYet (B0–B4) now have real implementations.
    // CostingStage and PackagingStage use StageResult.Failed (not IsNotImplemented) and are
    // covered by their own dedicated test classes.

    private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> Issues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "test", []);

        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => Issues;
    }
}
