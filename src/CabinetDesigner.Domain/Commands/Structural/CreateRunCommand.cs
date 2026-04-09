using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Domain.Commands.Structural;

public sealed record CreateRunCommand : DesignCommandBase
{
    public override string CommandType => "structural.create_run";

    public Point2D StartPoint { get; }

    public Point2D EndPoint { get; }

    public string WallId { get; }

    public CreateRunCommand(
        Point2D startPoint,
        Point2D endPoint,
        string wallId,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [wallId]))
    {
        StartPoint = startPoint;
        EndPoint = endPoint;
        WallId = wallId;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        List<ValidationIssue> issues = [];

        if (StartPoint.DistanceTo(EndPoint) <= Length.Zero)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "ZERO_LENGTH_RUN",
                "Run start and end points must be different."));
        }

        if (string.IsNullOrWhiteSpace(WallId))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_WALL",
                "A run must be associated with a wall."));
        }

        return issues;
    }
}
