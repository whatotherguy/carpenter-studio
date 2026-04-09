using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record EngineeringResolutionResult
{
    public required IReadOnlyList<AssemblyResolution> Assemblies { get; init; }

    public required IReadOnlyList<FillerRequirement> FillerRequirements { get; init; }

    public required IReadOnlyList<EndConditionUpdate> EndConditionUpdates { get; init; }
}

public sealed record AssemblyResolution(
    CabinetId CabinetId,
    string AssemblyType,
    IReadOnlyDictionary<string, string> ResolvedParameters);

public sealed record FillerRequirement(
    RunId RunId,
    Length Width,
    string Reason);

public sealed record EndConditionUpdate(
    RunId RunId,
    EndCondition LeftEndCondition,
    EndCondition RightEndCondition);
