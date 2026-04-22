using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands.Modification;

public sealed record ResizeCabinetCommand : DesignCommandBase
{
    public override string CommandType => "modification.resize_cabinet";

    public CabinetId CabinetId { get; }

    public Length NewWidth { get; }

    public Length NewDepth { get; }

    public Length NewHeight { get; }

    public Length PreviousNominalWidth { get; private set; }

    public Length NewNominalWidth => NewWidth;

    public bool HasExplicitDimensions { get; }

    public ResizeCabinetCommand(
        CabinetId cabinetId,
        Length newWidth,
        Length newDepth,
        Length newHeight,
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
        NewWidth = newWidth;
        NewDepth = newDepth;
        NewHeight = newHeight;
        PreviousNominalWidth = newWidth;
        HasExplicitDimensions = true;
    }

    internal ResizeCabinetCommand(
        CabinetId cabinetId,
        Length previousNominalWidth,
        Length newNominalWidth,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : this(
            cabinetId,
            newNominalWidth,
            Length.FromInches(1m),
            Length.FromInches(1m),
            origin,
            intentDescription,
            timestamp)
    {
        PreviousNominalWidth = previousNominalWidth;
        HasExplicitDimensions = false;
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

        if (NewWidth <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_WIDTH",
                "Width must be greater than zero."));
        }

        if (NewDepth <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_DEPTH",
                "Depth must be greater than zero."));
        }

        if (NewHeight <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_HEIGHT",
                "Height must be greater than zero."));
        }

        return issues;
    }
}
