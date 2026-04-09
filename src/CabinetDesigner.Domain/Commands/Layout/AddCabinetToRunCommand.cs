using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Layout;

public sealed record AddCabinetToRunCommand : DesignCommandBase
{
    public override string CommandType => "layout.add_cabinet_to_run";

    public RunId RunId { get; }

    public string CabinetTypeId { get; }

    public Length NominalWidth { get; }

    public RunPlacement Placement { get; }

    public int? InsertAtIndex { get; }

    public AddCabinetToRunCommand(
        RunId runId,
        string cabinetTypeId,
        Length nominalWidth,
        RunPlacement placement,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp,
        int? insertAtIndex = null)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [runId.Value.ToString()]))
    {
        RunId = runId;
        CabinetTypeId = cabinetTypeId;
        NominalWidth = nominalWidth;
        Placement = placement;
        InsertAtIndex = insertAtIndex;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        List<ValidationIssue> issues = [];

        if (NominalWidth <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_WIDTH",
                "Cabinet width must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(CabinetTypeId))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_TYPE",
                "Cabinet type ID is required."));
        }

        if (Placement == RunPlacement.AtIndex && InsertAtIndex is null)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_INDEX",
                "InsertAtIndex is required when Placement is AtIndex."));
        }

        return issues;
    }
}
