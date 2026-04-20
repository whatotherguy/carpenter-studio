using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Projection;

public sealed class InstallProjector : IInstallProjector
{
    private static readonly StringComparer KeyComparer = StringComparer.Ordinal;

    public InstallPlan Project(
        SpatialResolutionResult spatialResult,
        EngineeringResolutionResult engineeringResult,
        ManufacturingPlanResult manufacturingResult)
    {
        ArgumentNullException.ThrowIfNull(spatialResult);
        ArgumentNullException.ThrowIfNull(engineeringResult);
        ArgumentNullException.ThrowIfNull(manufacturingResult);

        if (!manufacturingResult.Plan.Readiness.IsReady)
        {
            return new InstallPlan
            {
                Steps = [],
                Dependencies = [],
                FasteningRequirements = [],
                Readiness = new InstallReadinessResult
                {
                    IsReady = false,
                    Blockers = [CreateManufacturingNotReadyBlocker(manufacturingResult.Plan)]
                }
            };
        }

        var readinessBlockers = BuildReadinessBlockers(
            spatialResult,
            engineeringResult,
            manufacturingResult.Plan);

        if (readinessBlockers.Count > 0)
        {
            return new InstallPlan
            {
                Steps = [],
                Dependencies = [],
                FasteningRequirements = [],
                Readiness = new InstallReadinessResult
                {
                    IsReady = false,
                    Blockers = readinessBlockers
                }
            };
        }

        var dependencies = new List<InstallDependency>();
        var seedSteps = new List<InstallStep>();
        var orderedCabinetStepsByRun = new Dictionary<RunId, IReadOnlyList<InstallStep>>();
        var assemblyLookup = engineeringResult.Assemblies.ToDictionary(
            assembly => assembly.CabinetId,
            assembly => assembly,
            EqualityComparer<CabinetId>.Default);

        foreach (var runGroup in spatialResult.Placements
                     .GroupBy(placement => placement.RunId)
                     .OrderBy(group => group.Min(GetRunOffset))
                     .ThenBy(group => group.Min(placement => placement.Origin.Y))
                     .ThenBy(group => group.Min(placement => placement.Origin.X))
                     .ThenBy(group => group.Key.Value))
        {
            var orderedPlacements = runGroup
                .OrderBy(GetRunOffset)
                .ThenBy(placement => placement.Origin.Y)
                .ThenBy(placement => placement.Origin.X)
                .ThenBy(placement => placement.CabinetId.Value)
                .ToArray();

            var runSteps = new List<InstallStep>(orderedPlacements.Length);

            for (var index = 0; index < orderedPlacements.Length; index++)
            {
                var placement = orderedPlacements[index];
                var assembly = assemblyLookup[placement.CabinetId];
                var step = new InstallStep
                {
                    StepKey = CreateCabinetStepKey(placement.CabinetId),
                    Order = -1,
                    Kind = InstallStepKind.CabinetInstall,
                    CabinetId = placement.CabinetId,
                    RunId = placement.RunId,
                    SequenceGroupIndex = index,
                    Footprint = placement.WorldBounds,
                    Description = $"Install cabinet {placement.CabinetId.Value:D} using {assembly.AssemblyType}.",
                    DependsOn = [],
                    Rationales =
                    [
                        InstallRationale.Create(
                            "install.step.cabinet_from_engineering_and_manufacturing",
                            "Install step is derived from approved placement, engineering assembly resolution, and manufactured parts.",
                            new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["assembly_type"] = assembly.AssemblyType,
                                ["cabinet_id"] = placement.CabinetId.Value.ToString("D"),
                                ["run_id"] = placement.RunId.Value.ToString("D")
                            })
                    ]
                };

                runSteps.Add(step);
                seedSteps.Add(step);

                if (index == 0)
                {
                    continue;
                }

                var prerequisite = runSteps[index - 1];
                dependencies.Add(new InstallDependency
                {
                    PrerequisiteStepKey = prerequisite.StepKey,
                    DependentStepKey = step.StepKey,
                    Reason = InstallDependencyReason.RunSequence,
                    Rationale = InstallRationale.Create(
                        "install.dependency.run_sequence",
                        "Install order follows the deterministic approved run order.",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["run_id"] = placement.RunId.Value.ToString("D"),
                            ["prerequisite_step"] = prerequisite.StepKey,
                            ["dependent_step"] = step.StepKey
                        })
                });
            }

            orderedCabinetStepsByRun[runGroup.Key] = runSteps;
        }

        foreach (var fillerStep in BuildFillerSteps(engineeringResult, orderedCabinetStepsByRun))
        {
            seedSteps.Add(fillerStep.Step);
            dependencies.AddRange(fillerStep.Dependencies);
        }

        var dependencyLookup = dependencies
            .GroupBy(dependency => dependency.DependentStepKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(dependency => dependency.PrerequisiteStepKey)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        try
        {
            var orderedSteps = InstallSequencer.Sequence(seedSteps, dependencies)
                .Select(step => step with
                {
                    DependsOn = dependencyLookup.TryGetValue(step.StepKey, out var stepDependsOn)
                        ? stepDependsOn
                        : []
                })
                .ToArray();

            return new InstallPlan
            {
                Steps = orderedSteps,
                Dependencies = dependencies
                    .OrderBy(dependency => dependency.PrerequisiteStepKey, StringComparer.Ordinal)
                    .ThenBy(dependency => dependency.DependentStepKey, StringComparer.Ordinal)
                    .ThenBy(dependency => dependency.Reason)
                    .ToArray(),
                FasteningRequirements = BuildFasteningRequirements(orderedSteps, manufacturingResult.Plan),
                Readiness = new InstallReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            };
        }
        catch (InvalidOperationException exception)
        {
            return new InstallPlan
            {
                Steps = [],
                Dependencies = dependencies
                    .OrderBy(dependency => dependency.PrerequisiteStepKey, StringComparer.Ordinal)
                    .ThenBy(dependency => dependency.DependentStepKey, StringComparer.Ordinal)
                    .ThenBy(dependency => dependency.Reason)
                    .ToArray(),
                FasteningRequirements = [],
                Readiness = new InstallReadinessResult
                {
                    IsReady = false,
                    Blockers =
                    [
                        new InstallBlocker
                        {
                            Code = InstallBlockerCode.DependencyCycle,
                            Message = exception.Message,
                            AffectedEntityIds = seedSteps
                                .Select(step => step.StepKey)
                                .OrderBy(key => key, StringComparer.Ordinal)
                                .ToArray()
                        }
                    ]
                }
            };
        }
    }

    private static decimal GetRunOffset(RunPlacement placement)
    {
        var originVector = new Vector2D(placement.Origin.X, placement.Origin.Y);
        return originVector.Dot(placement.Direction);
    }

    private static InstallBlocker CreateManufacturingNotReadyBlocker(ManufacturingPlan manufacturingPlan)
    {
        var blockerCodes = manufacturingPlan.Readiness.Blockers
            .Select(blocker => blocker.Code.ToString())
            .Distinct(KeyComparer)
            .OrderBy(code => code, KeyComparer)
            .ToArray();
        var blockerSummary = blockerCodes.Length == 0
            ? "unknown blockers"
            : string.Join(", ", blockerCodes);

        return new InstallBlocker
        {
            Code = InstallBlockerCode.ManufacturingNotReady,
            Message = $"Manufacturing plan is not ready; resolve manufacturing blockers before install planning. Current blockers: {blockerSummary}.",
            AffectedEntityIds = manufacturingPlan.Readiness.Blockers
                .SelectMany(blocker => blocker.AffectedEntityIds)
                .Distinct(KeyComparer)
                .OrderBy(id => id, KeyComparer)
                .ToArray()
        };
    }

    private static IReadOnlyList<InstallBlocker> BuildReadinessBlockers(
        SpatialResolutionResult spatialResult,
        EngineeringResolutionResult engineeringResult,
        ManufacturingPlan manufacturingPlan)
    {
        var blockers = new List<InstallBlocker>();
        var placements = spatialResult.Placements
            .OrderBy(placement => placement.RunId.Value)
            .ThenBy(GetRunOffset)
            .ThenBy(placement => placement.Origin.Y)
            .ThenBy(placement => placement.Origin.X)
            .ThenBy(placement => placement.CabinetId.Value)
            .ToArray();
        var assemblyCabinetIds = engineeringResult.Assemblies
            .Select(assembly => assembly.CabinetId)
            .ToHashSet();
        var cutListCabinetIds = manufacturingPlan.CutList
            .Select(item => item.CabinetId)
            .ToHashSet();
        var runsWithEndConditions = engineeringResult.EndConditionUpdates
            .Select(update => update.RunId)
            .ToHashSet();

        if (manufacturingPlan.CutList.Count == 0)
        {
            blockers.Add(new InstallBlocker
            {
                Code = InstallBlockerCode.MissingManufacturingCutList,
                Message = "Manufacturing plan produced no cut-list parts; install planning cannot verify cabinet readiness.",
                AffectedEntityIds = []
            });
        }

        foreach (var placement in placements)
        {
            if (!assemblyCabinetIds.Contains(placement.CabinetId))
            {
                blockers.Add(new InstallBlocker
                {
                    Code = InstallBlockerCode.MissingEngineeringAssembly,
                    Message = $"Cabinet '{placement.CabinetId.Value:D}' is missing an engineering assembly; install planning cannot determine a safe install sequence.",
                    AffectedEntityIds = [placement.CabinetId.Value.ToString("D")]
                });
            }

            if (!runsWithEndConditions.Contains(placement.RunId))
            {
                blockers.Add(new InstallBlocker
                {
                    Code = InstallBlockerCode.MissingRunEndConditions,
                    Message = $"Run '{placement.RunId.Value:D}' is missing engineered end conditions; install planning cannot determine safe completion requirements.",
                    AffectedEntityIds = [placement.RunId.Value.ToString("D")]
                });
            }

            if (!cutListCabinetIds.Contains(placement.CabinetId))
            {
                blockers.Add(new InstallBlocker
                {
                    Code = InstallBlockerCode.MissingCabinetManufacturingParts,
                    Message = $"Cabinet '{placement.CabinetId.Value:D}' has no manufactured parts in the cut list; install planning cannot verify fastening readiness.",
                    AffectedEntityIds = [placement.CabinetId.Value.ToString("D")]
                });
            }
        }

        var runIdsWithPlacements = placements
            .Select(placement => placement.RunId)
            .ToHashSet();

        foreach (var requirement in engineeringResult.FillerRequirements
                     .OrderBy(requirement => requirement.RunId.Value)
                     .ThenBy(requirement => requirement.Width.Inches)
                     .ThenBy(requirement => requirement.Reason, KeyComparer))
        {
            if (runIdsWithPlacements.Contains(requirement.RunId))
            {
                continue;
            }

            blockers.Add(new InstallBlocker
            {
                Code = InstallBlockerCode.UnsupportedEngineeringFiller,
                Message = $"Run '{requirement.RunId.Value:D}' has filler requirements but no cabinet placements; filler installation is unsupported without installed cabinets.",
                AffectedEntityIds = [requirement.RunId.Value.ToString("D")]
            });
        }

        return blockers
            .GroupBy(
                blocker => (blocker.Code, blocker.Message, string.Join("|", blocker.AffectedEntityIds.OrderBy(id => id, KeyComparer))),
                EqualityComparer<(InstallBlockerCode, string, string)>.Default)
            .Select(group => group.First() with
            {
                AffectedEntityIds = group
                    .SelectMany(blocker => blocker.AffectedEntityIds)
                    .Distinct(KeyComparer)
                    .OrderBy(id => id, KeyComparer)
                    .ToArray()
            })
            .OrderBy(blocker => blocker.Code)
            .ThenBy(blocker => blocker.Message, KeyComparer)
            .ThenBy(blocker => blocker.AffectedEntityIds.Count > 0 ? blocker.AffectedEntityIds[0] : string.Empty, KeyComparer)
            .ToArray();
    }

    private static string CreateCabinetStepKey(CabinetId cabinetId) => $"cabinet:{cabinetId.Value:D}";

    private static IReadOnlyList<FillerStepProjection> BuildFillerSteps(
        EngineeringResolutionResult engineeringResult,
        IReadOnlyDictionary<RunId, IReadOnlyList<InstallStep>> orderedCabinetStepsByRun)
    {
        var result = new List<FillerStepProjection>();

        foreach (var runGroup in engineeringResult.FillerRequirements
                     .GroupBy(requirement => requirement.RunId)
                     .OrderBy(group => group.Key.Value))
        {
            orderedCabinetStepsByRun.TryGetValue(runGroup.Key, out var runCabinets);
            runCabinets ??= [];
            var fillerIndex = 0;
            var fillerOffset = runCabinets.Count == 0 ? 0 : runCabinets.Max(step => step.SequenceGroupIndex) + 1;

            foreach (var requirement in runGroup
                         .OrderBy(requirement => requirement.Width.Inches)
                         .ThenBy(requirement => requirement.Reason, StringComparer.Ordinal))
            {
                var stepKey = $"filler:{runGroup.Key.Value:D}:{fillerIndex}";
                var footprint = runCabinets.Count == 0
                    ? new Rect2D(Point2D.Origin, requirement.Width, Length.Zero)
                    : new Rect2D(
                        runCabinets[^1].Footprint.Origin,
                        requirement.Width,
                        runCabinets[^1].Footprint.Height);
                var step = new InstallStep
                {
                    StepKey = stepKey,
                    Order = -1,
                    Kind = InstallStepKind.FillerInstall,
                    CabinetId = null,
                    RunId = runGroup.Key,
                    SequenceGroupIndex = fillerOffset + fillerIndex,
                    Footprint = footprint,
                    Description = $"Install filler on run {runGroup.Key.Value:D}.",
                    DependsOn = [],
                    Rationales =
                    [
                        InstallRationale.Create(
                            "install.step.filler_from_engineering",
                            "Filler install step is derived from an engineering filler requirement.",
                            new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["run_id"] = runGroup.Key.Value.ToString("D"),
                                ["reason"] = requirement.Reason,
                                ["width_in"] = requirement.Width.Inches.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            })
                    ]
                };

                var dependencies = runCabinets
                    .Select(cabinetStep => new InstallDependency
                    {
                        PrerequisiteStepKey = cabinetStep.StepKey,
                        DependentStepKey = stepKey,
                        Reason = InstallDependencyReason.EngineeringRequirement,
                        Rationale = InstallRationale.Create(
                            "install.dependency.filler_after_run_cabinets",
                            "Engineering filler output is installed after the run cabinets it completes.",
                            new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["run_id"] = runGroup.Key.Value.ToString("D"),
                                ["reason"] = requirement.Reason,
                                ["prerequisite_step"] = cabinetStep.StepKey,
                                ["dependent_step"] = stepKey
                            })
                    })
                    .ToArray();

                result.Add(new FillerStepProjection(step, dependencies));
                fillerIndex++;
            }
        }

        return result;
    }

    private static IReadOnlyList<FasteningRequirement> BuildFasteningRequirements(
        IReadOnlyList<InstallStep> orderedSteps,
        ManufacturingPlan manufacturingPlan)
    {
        var cutListCounts = manufacturingPlan.CutList
            .GroupBy(item => item.CabinetId)
            .ToDictionary(group => group.Key, group => group.Count());

        return orderedSteps
            .Where(step => step.CabinetId is not null)
            .Select(step =>
            {
                var cabinetId = step.CabinetId!.Value;
                cutListCounts.TryGetValue(cabinetId, out var partCount);

                return new FasteningRequirement
                {
                    CabinetId = cabinetId,
                    FasteningType = "ThroughBack",
                    Location = step.Footprint,
                    Requirements = $"Verify {partCount} manufactured part(s) before fastening.",
                    Rationale = InstallRationale.Create(
                        "install.fastening.from_manufacturing",
                        "Fastening requirement is derived from approved placement and manufacturing output.",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["cabinet_id"] = cabinetId.Value.ToString("D"),
                            ["part_count"] = partCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        })
                };
            })
            .OrderBy(requirement => requirement.CabinetId.Value)
            .ToArray();
    }

    private sealed record FillerStepProjection(
        InstallStep Step,
        IReadOnlyList<InstallDependency> Dependencies);
}
