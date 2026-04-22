using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Modification;

public sealed record AddOpeningCommand : DesignCommandBase
{
    public override string CommandType => "modification.add_opening";

    public CabinetId CabinetId { get; }

    public OpeningType OpeningType { get; }

    public Length Width { get; }

    public Length Height { get; }

    public int? InsertIndex { get; }

    public AddOpeningCommand(
        CabinetId cabinetId,
        OpeningType openingType,
        Length width,
        Length height,
        int? insertIndex,
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
        OpeningType = openingType;
        Width = width;
        Height = height;
        InsertIndex = insertIndex;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        List<ValidationIssue> issues = [];

        if (CabinetId == default)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_CABINET",
                "A cabinet identifier is required."));
        }

        if (Width <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_WIDTH",
                "Opening width must be greater than zero."));
        }

        if (Height <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_HEIGHT",
                "Opening height must be greater than zero."));
        }

        if (InsertIndex is < 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_OPENING_INDEX",
                "Opening insert index cannot be negative."));
        }

        return issues;
    }
}
