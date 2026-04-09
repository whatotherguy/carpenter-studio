using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Modification;

public sealed record ResizeCabinetCommand : DesignCommandBase
{
    public override string CommandType => "modification.resize_cabinet";

    public CabinetId CabinetId { get; }

    public Length NewNominalWidth { get; }

    public Length PreviousNominalWidth { get; }

    public ResizeCabinetCommand(
        CabinetId cabinetId,
        Length previousNominalWidth,
        Length newNominalWidth,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [cabinetId.Value.ToString()]))
    {
        CabinetId = cabinetId;
        NewNominalWidth = newNominalWidth;
        PreviousNominalWidth = previousNominalWidth;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        List<ValidationIssue> issues = [];

        if (NewNominalWidth <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_WIDTH",
                "New width must be greater than zero."));
        }

        if (NewNominalWidth == PreviousNominalWidth)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "NO_CHANGE",
                "New width is the same as the current width."));
        }

        return issues;
    }
}
