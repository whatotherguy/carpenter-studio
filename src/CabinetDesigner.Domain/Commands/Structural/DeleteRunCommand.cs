using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Structural;

public sealed record DeleteRunCommand : DesignCommandBase
{
    public override string CommandType => "structural.delete_run";

    public RunId RunId { get; }

    public DeleteRunCommand(
        RunId runId,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [runId.Value.ToString()]))
    {
        RunId = runId;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        if (RunId == default)
        {
            return
            [
                new ValidationIssue(
                    ValidationSeverity.Error,
                    "MISSING_RUN",
                    "A run identifier is required.")
            ];
        }

        return [];
    }
}
