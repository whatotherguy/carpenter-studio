using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Layout;

public sealed record InsertCabinetIntoRunCommand : DesignCommandBase
{
    public override string CommandType => "layout.insert_cabinet_into_run";

    public RunId RunId { get; }

    public string CabinetTypeId { get; }

    public Length NominalWidth { get; }

    public Length NominalDepth { get; }

    public int InsertAtIndex { get; }

    public CabinetId LeftNeighborId { get; }

    public CabinetId RightNeighborId { get; }

    public CabinetCategory Category { get; }

    public ConstructionMethod Construction { get; }

    public InsertCabinetIntoRunCommand(
        RunId runId,
        string cabinetTypeId,
        Length nominalWidth,
        int insertAtIndex,
        CabinetId leftNeighborId,
        CabinetId rightNeighborId,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp,
        Length? nominalDepth = null,
        CabinetCategory category = CabinetCategory.Base,
        ConstructionMethod construction = ConstructionMethod.Frameless)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [runId.Value.ToString(), leftNeighborId.Value.ToString(), rightNeighborId.Value.ToString()]))
    {
        RunId = runId;
        CabinetTypeId = cabinetTypeId;
        NominalWidth = nominalWidth;
        NominalDepth = nominalDepth ?? Length.FromInches(24m);
        InsertAtIndex = insertAtIndex;
        LeftNeighborId = leftNeighborId;
        RightNeighborId = rightNeighborId;
        Category = category;
        Construction = construction;
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

        if (NominalDepth <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_DEPTH",
                "Cabinet depth must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(CabinetTypeId))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_TYPE",
                "Cabinet type ID is required."));
        }

        if (InsertAtIndex < 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_INDEX",
                "Insert index cannot be negative."));
        }

        return issues;
    }
}
