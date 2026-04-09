using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Layout;

public sealed record MoveCabinetCommand : DesignCommandBase
{
    public override string CommandType => "layout.move_cabinet";

    public CabinetId CabinetId { get; }

    public RunId SourceRunId { get; }

    public RunId TargetRunId { get; }

    public RunPlacement TargetPlacement { get; }

    public int? TargetIndex { get; }

    public MoveCabinetCommand(
        CabinetId cabinetId,
        RunId sourceRunId,
        RunId targetRunId,
        RunPlacement targetPlacement,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp,
        int? targetIndex = null)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [cabinetId.Value.ToString(), sourceRunId.Value.ToString(), targetRunId.Value.ToString()]))
    {
        CabinetId = cabinetId;
        SourceRunId = sourceRunId;
        TargetRunId = targetRunId;
        TargetPlacement = targetPlacement;
        TargetIndex = targetIndex;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        List<ValidationIssue> issues = [];

        if (TargetPlacement == RunPlacement.AtIndex && TargetIndex is null)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_INDEX",
                "TargetIndex is required when TargetPlacement is AtIndex."));
        }

        return issues;
    }
}
