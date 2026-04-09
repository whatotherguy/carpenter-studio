using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.InstallContext;

public sealed record InstallPlan
{
    public required IReadOnlyList<InstallStep> Steps { get; init; }

    public required IReadOnlyList<InstallDependency> Dependencies { get; init; }

    public required IReadOnlyList<FasteningRequirement> FasteningRequirements { get; init; }

    public required InstallReadinessResult Readiness { get; init; }
}

public sealed record InstallStep
{
    public required string StepKey { get; init; }

    public required int Order { get; init; }

    public required InstallStepKind Kind { get; init; }

    public CabinetId? CabinetId { get; init; }

    public required RunId RunId { get; init; }

    public required int SequenceGroupIndex { get; init; }

    public required Rect2D Footprint { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> DependsOn { get; init; }

    public required IReadOnlyList<InstallRationale> Rationales { get; init; }
}

public enum InstallStepKind
{
    CabinetInstall,
    FillerInstall
}

public sealed record InstallDependency
{
    public required string PrerequisiteStepKey { get; init; }

    public required string DependentStepKey { get; init; }

    public required InstallDependencyReason Reason { get; init; }

    public required InstallRationale Rationale { get; init; }
}

public enum InstallDependencyReason
{
    RunSequence,
    EngineeringRequirement
}

public sealed record InstallRationale
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyDictionary<string, string> Context { get; init; }

    public static InstallRationale Create(
        string code,
        string message,
        IReadOnlyDictionary<string, string>? context = null) =>
        new()
        {
            Code = code,
            Message = message,
            Context = context ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
}

public sealed record FasteningRequirement
{
    public required CabinetId CabinetId { get; init; }

    public required string FasteningType { get; init; }

    public required Rect2D Location { get; init; }

    public required string Requirements { get; init; }

    public required InstallRationale Rationale { get; init; }
}

public sealed record InstallReadinessResult
{
    public required bool IsReady { get; init; }

    public required IReadOnlyList<InstallBlocker> Blockers { get; init; }
}

public sealed record InstallBlocker
{
    public required InstallBlockerCode Code { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyList<string> AffectedEntityIds { get; init; }

    public ValidationIssue ToValidationIssue() =>
        new(
            ValidationSeverity.Error,
            $"install.{Code}",
            Message,
            AffectedEntityIds);
}

public enum InstallBlockerCode
{
    ManufacturingNotReady,
    DependencyCycle
}

public static class InstallSequencer
{
    public static IReadOnlyList<InstallStep> Sequence(
        IReadOnlyList<InstallStep> steps,
        IReadOnlyList<InstallDependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(dependencies);

        var stepByKey = steps.ToDictionary(step => step.StepKey, StringComparer.Ordinal);
        var inDegree = steps.ToDictionary(step => step.StepKey, _ => 0, StringComparer.Ordinal);
        var adjacency = steps.ToDictionary(
            step => step.StepKey,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var dependency in dependencies)
        {
            if (!stepByKey.ContainsKey(dependency.PrerequisiteStepKey) ||
                !stepByKey.ContainsKey(dependency.DependentStepKey))
            {
                throw new InvalidOperationException("Install dependency references an unknown step.");
            }

            inDegree[dependency.DependentStepKey]++;
            adjacency[dependency.PrerequisiteStepKey].Add(dependency.DependentStepKey);
        }

        var ready = new SortedSet<InstallStep>(
            steps.Where(step => inDegree[step.StepKey] == 0),
            InstallStepOrderComparer.Instance);
        var ordered = new List<InstallStep>(steps.Count);
        var order = 0;

        while (ready.Count > 0)
        {
            var current = ready.Min!;
            ready.Remove(current);
            ordered.Add(current with { Order = order++ });

            foreach (var dependentKey in adjacency[current.StepKey]
                         .OrderBy(key => key, StringComparer.Ordinal))
            {
                inDegree[dependentKey]--;
                if (inDegree[dependentKey] == 0)
                {
                    ready.Add(stepByKey[dependentKey]);
                }
            }
        }

        if (ordered.Count != steps.Count)
        {
            throw new InvalidOperationException(
                $"Install dependency graph contains a cycle. Sequenced {ordered.Count} of {steps.Count} steps.");
        }

        return ordered;
    }

    private sealed class InstallStepOrderComparer : IComparer<InstallStep>
    {
        public static InstallStepOrderComparer Instance { get; } = new();

        public int Compare(InstallStep? x, InstallStep? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var runCompare = x.RunId.Value.CompareTo(y.RunId.Value);
            if (runCompare != 0)
            {
                return runCompare;
            }

            var sequenceCompare = x.SequenceGroupIndex.CompareTo(y.SequenceGroupIndex);
            if (sequenceCompare != 0)
            {
                return sequenceCompare;
            }

            var kindCompare = GetPriority(x.Kind).CompareTo(GetPriority(y.Kind));
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            return string.Compare(x.StepKey, y.StepKey, StringComparison.Ordinal);
        }

        private static int GetPriority(InstallStepKind kind) =>
            kind switch
            {
                InstallStepKind.CabinetInstall => 0,
                InstallStepKind.FillerInstall => 1,
                _ => 99
            };
    }
}
