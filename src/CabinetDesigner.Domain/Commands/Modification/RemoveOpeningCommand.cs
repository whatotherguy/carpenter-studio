using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Modification;

public sealed record RemoveOpeningCommand : DesignCommandBase
{
    public override string CommandType => "modification.remove_opening";

    public CabinetId CabinetId { get; }

    public OpeningId OpeningId { get; }

    public RemoveOpeningCommand(
        CabinetId cabinetId,
        OpeningId openingId,
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

        return issues;
    }
}
