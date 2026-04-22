using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Modification;

public sealed record SetCabinetCategoryCommand : DesignCommandBase
{
    public override string CommandType => "modification.set_cabinet_category";

    public CabinetId CabinetId { get; }

    public CabinetCategory Category { get; }

    public SetCabinetCategoryCommand(
        CabinetId cabinetId,
        CabinetCategory category,
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
        Category = category;
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

        return issues;
    }
}
