using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Modification;

public sealed record RemoveCabinetOverrideCommand : DesignCommandBase
{
    public override string CommandType => "modification.remove_cabinet_override";

    public CabinetId CabinetId { get; }

    public string OverrideKey { get; }

    public RemoveCabinetOverrideCommand(
        CabinetId cabinetId,
        string overrideKey,
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
        OverrideKey = overrideKey;
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

        if (string.IsNullOrWhiteSpace(OverrideKey) || OverrideKey.Any(char.IsWhiteSpace))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_OVERRIDE_KEY",
                "Override key is required and cannot contain whitespace."));
        }

        return issues;
    }
}
