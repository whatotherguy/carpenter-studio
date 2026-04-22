using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Modification;

public sealed record ReorderOpeningCommand : DesignCommandBase
{
    public override string CommandType => "modification.reorder_opening";

    public CabinetId CabinetId { get; }

    public OpeningId OpeningId { get; }

    public int NewIndex { get; }

    public ReorderOpeningCommand(
        CabinetId cabinetId,
        OpeningId openingId,
        int newIndex,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [cabinetId.Value.ToString(), openingId.Value.ToString()]))
    {
        CabinetId = cabinetId;
        OpeningId = openingId;
        NewIndex = newIndex;
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

        if (OpeningId == default)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_OPENING",
                "An opening identifier is required."));
        }

        if (NewIndex < 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_OPENING_INDEX",
                "Opening index cannot be negative."));
        }

        return issues;
    }
}
