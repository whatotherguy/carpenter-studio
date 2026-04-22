using System.Globalization;
using System.Xml.Linq;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.Export;
using CabinetDesigner.Application.Packaging;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Domain.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CabinetDesigner.Tests.Acceptance;

public sealed class V1AcceptanceTests
{
    [Fact]
    public void FullV1Workflow_CreateProjectAddRoomPlaceCabinetsExportCutList_Succeeds()
    {
        using var provider = CreateProvider();
        SeedSampleProject(provider.GetRequiredService<CurrentWorkingRevisionSource>());

        var context = ExecuteFullPipeline(provider);

        Assert.True(context.PartResult.Parts.Count > 0, "Expected Part Generation to produce at least one part.");
        Assert.Equal(CostingStatus.NotConfigured, context.CostingResult.Status);

        var state = provider.GetRequiredService<ICurrentPersistedProjectState>().CurrentState!;
        var exporter = provider.GetRequiredService<ICutListExporter>();
        var export = exporter.Export(new CutListExportRequest(
            context.ManufacturingResult.Plan,
            BuildSummary(state),
            context.ConstraintResult.MaterialAssignments));

        Assert.NotEmpty(export.Csv);
        Assert.NotEmpty(export.Txt);
        Assert.NotEmpty(export.Html);
        Assert.False(string.IsNullOrWhiteSpace(export.ContentHash));
    }

    [Fact]
    public void FullV1Workflow_IsDeterministic()
    {
        var firstRun = RunSampleWorkflow();
        var secondRun = RunSampleWorkflow();

        Assert.Equal(firstRun.PartResultBytes, secondRun.PartResultBytes);
        Assert.Equal(firstRun.ManufacturingPlanBytes, secondRun.ManufacturingPlanBytes);
        Assert.Equal(firstRun.Export.ContentHash, secondRun.Export.ContentHash);
        Assert.Equal(firstRun.PackagingHash, secondRun.PackagingHash);
    }

    [Fact]
    public void FullV1Workflow_InvalidDesign_PackagingRejected()
    {
        var context = BuildOverCapacityContext();

        new ValidationStage().Execute(context);
        Assert.False(context.ValidationResult.Result.IsValid);

        var result = new PackagingStage(
            new FixedStateSource(CreatePackagingState()),
            new FixedClock(DateTimeOffset.Parse("2026-04-21T18:00:00Z", CultureInfo.InvariantCulture)))
            .Execute(context);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("PACKAGING_INVALID_DESIGN", issue.Code);
    }

    [Fact]
    public void V1CodeHealth_NoNotImplementedExceptionInSrc()
    {
        var srcRoot = LocateRepoSubdirectory("src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            if (content.Contains("throw new NotImplementedException", StringComparison.Ordinal)
                || content.Contains("catch (NotImplementedException", StringComparison.Ordinal))
            {
                offenders.Add(file);
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void V1Layering_PresentationDoesNotReferenceDomain()
    {
        var csprojPath = Path.Combine(
            LocateRepoSubdirectory("src"),
            "CabinetDesigner.Presentation",
            "CabinetDesigner.Presentation.csproj");

        var document = XDocument.Load(csprojPath);
        var domainReferences = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(value => value.Contains("CabinetDesigner.Domain.csproj", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(domainReferences);
    }

    [Fact]
    public void V1CostingDecoupled_PipelineSucceeds_WhenNoPricingConfigured()
    {
        using var provider = CreateProvider();
        Assert.False(provider.GetRequiredService<ICatalogService>().IsPricingConfigured);
        SeedSampleProject(provider.GetRequiredService<CurrentWorkingRevisionSource>());

        var context = ExecuteFullPipeline(provider);

        Assert.Equal(CostingStatus.NotConfigured, context.CostingResult.Status);
        Assert.Equal(0m, context.CostingResult.Total);
    }

    private static WorkflowOutputs RunSampleWorkflow()
    {
        using var provider = CreateProvider();
        SeedSampleProject(provider.GetRequiredService<CurrentWorkingRevisionSource>());

        var context = ExecuteFullPipeline(provider);

        var state = provider.GetRequiredService<ICurrentPersistedProjectState>().CurrentState!;
        var export = provider.GetRequiredService<ICutListExporter>().Export(new CutListExportRequest(
            context.ManufacturingResult.Plan,
            BuildSummary(state),
            context.ConstraintResult.MaterialAssignments));

        var packaging = provider.GetRequiredService<IPackagingResultStore>().Current
            ?? throw new InvalidOperationException("Packaging result was not stored.");

        return new WorkflowOutputs(
            DeterministicJson.Serialize(context.PartResult),
            DeterministicJson.Serialize(context.ManufacturingResult.Plan),
            export,
            packaging.ContentHash);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddSingleton<IPreviousApprovedCostLookup, NullPreviousApprovedCostLookup>();
        return services.BuildServiceProvider();
    }

    private static ResolutionContext ExecuteFullPipeline(ServiceProvider provider)
    {
        var stateStore = provider.GetRequiredService<IDesignStateStore>();
        var catalogService = provider.GetRequiredService<ICatalogService>();
        var costingPolicy = provider.GetRequiredService<ICostingPolicy>();
        var workingRevisionSource = provider.GetRequiredService<IWorkingRevisionSource>();
        var projectState = provider.GetRequiredService<ICurrentPersistedProjectState>();
        var validationStore = provider.GetRequiredService<IValidationResultStore>();
        var packagingStore = provider.GetRequiredService<IPackagingResultStore>();
        var previousCostLookup = provider.GetRequiredService<IPreviousApprovedCostLookup>();
        var clock = provider.GetRequiredService<IClock>();

        var stages = new IResolutionStage[]
        {
            new InputCaptureStage(stateStore),
            new InteractionInterpretationStage(new InMemoryDeltaTracker(), stateStore, catalogService),
            new SpatialResolutionStage(stateStore),
            new EngineeringResolutionStage(stateStore),
            new PartGenerationStage(stateStore),
            new ConstraintPropagationStage(catalogService, stateStore),
            new ManufacturingPlanningStage(),
            new InstallPlanningStage(),
            new CostingStage(catalogService, costingPolicy, previousCostLookup: previousCostLookup),
            new ValidationStage(validationStore, projectState),
            new PackagingStage(workingRevisionSource, clock, packagingStore)
        };

        var context = new ResolutionContext
        {
            Command = new RebuildCurrentProjectCommand(),
            Mode = ResolutionMode.Full
        };

        foreach (var stage in stages)
        {
            var result = stage.Execute(context);
            context.AccumulatedIssues.AddRange(result.Issues);
            context.ExplanationNodeIds.AddRange(result.ExplanationNodeIds);
            Assert.True(
                result.Success,
                $"Stage {stage.StageNumber} ({stage.StageName}) failed: {FormatIssues(result.Issues)}");
        }

        return context;
    }

    private static ProjectSummary BuildSummary(PersistedProjectState state) =>
        new(
            state.Project.Name,
            state.Revision.Label ?? $"Rev {state.Revision.RevisionNumber}",
            state.Revision.CreatedAt.ToUniversalTime(),
            "Carpenter Studio");

    private static string FormatIssues(IReadOnlyList<ValidationIssue> issues) =>
        string.Join("; ", issues.Select(issue => $"{issue.Code}: {issue.Message}"));

    private static void SeedSampleProject(CurrentWorkingRevisionSource currentState)
    {
        var createdAt = DateTimeOffset.Parse("2026-04-21T18:00:00Z", CultureInfo.InvariantCulture);
        var projectId = new ProjectId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var revisionId = new RevisionId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var roomId = new RoomId(Guid.Parse("50000000-0000-0000-0000-000000000001"));
        var wallId = new WallId(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("80000000-0000-0000-0000-000000000001"));
        var framelessCabinetId = new CabinetId(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        var faceFrameCabinetId = new CabinetId(Guid.Parse("a0000000-0000-0000-0000-000000000002"));

        var project = new ProjectRecord(projectId, "V1 Acceptance Project", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");

        var room = new Room(roomId, revisionId, "Kitchen", Length.FromInches(96m));
        var wall = new Wall(wallId, roomId, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(runId, wall.Id, Length.FromInches(96m));
        run.AppendCabinet(framelessCabinetId, Length.FromInches(24m));
        run.AppendCabinet(faceFrameCabinetId, Length.FromInches(24m));

        var framelessCabinet = new Cabinet(
            framelessCabinetId,
            revisionId,
            "base-standard-24",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            Length.FromInches(24m),
            Length.FromInches(24m),
            Length.FromInches(34.5m));
        var faceFrameCabinet = new Cabinet(
            faceFrameCabinetId,
            revisionId,
            "base-faceframe-24",
            CabinetCategory.Base,
            ConstructionMethod.FaceFrame,
            Length.FromInches(24m),
            Length.FromInches(24m),
            Length.FromInches(34.5m));

        var workingRevision = new WorkingRevision(
            revision,
            [room],
            [wall],
            [run],
            [framelessCabinet, faceFrameCabinet],
            []);
        var checkpoint = new AutosaveCheckpoint("v1-acceptance", projectId, revisionId, createdAt, null, true);

        currentState.SetCurrentState(new PersistedProjectState(project, revision, workingRevision, checkpoint));
    }

    private static ResolutionContext BuildOverCapacityContext()
    {
        var cabinetId = new CabinetId(Guid.Parse("c0000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("d0000000-0000-0000-0000-000000000001"));
        var materialId = new MaterialId(Guid.Parse("50000000-0000-0000-0000-000000000099"));

        var part = new GeneratedPart
        {
            PartId = "part:invalid:1",
            CabinetId = cabinetId,
            PartType = "LeftSide",
            Width = Length.FromInches(30m),
            Height = Length.FromInches(30m),
            MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
            MaterialId = materialId,
            GrainDirection = GrainDirection.LengthWise,
            Edges = new EdgeTreatment(null, null, null, null),
            Label = "Over Capacity Cabinet-LeftSide"
        };
        var cutListItem = new CutListItem
        {
            PartId = part.PartId,
            CabinetId = cabinetId,
            PartType = part.PartType,
            Label = part.Label,
            CutWidth = part.Width,
            CutHeight = part.Height,
            MaterialThickness = part.MaterialThickness,
            MaterialId = part.MaterialId,
            GrainDirection = part.GrainDirection,
            EdgeTreatment = new ManufacturedEdgeTreatment(null, null, null, null)
        };

        return new ResolutionContext
        {
            Command = new RebuildCurrentProjectCommand(),
            Mode = ResolutionMode.Full,
            SpatialResult = new SpatialResolutionResult
            {
                SlotPositionUpdates = [],
                AdjacencyChanges = [],
                RunSummaries =
                [
                    new RunSummary(runId, Length.FromInches(24m), Length.FromInches(30m), Length.Zero, 1)
                ],
                Placements = []
            },
            EngineeringResult = new EngineeringResolutionResult
            {
                Assemblies = [],
                FillerRequirements = [],
                EndConditionUpdates = []
            },
            ConstraintResult = new ConstraintPropagationResult
            {
                MaterialAssignments = [],
                HardwareAssignments = [],
                Violations = []
            },
            PartResult = new PartGenerationResult
            {
                Parts = [part]
            },
            ManufacturingResult = new ManufacturingPlanResult
            {
                Plan = new ManufacturingPlan
                {
                    MaterialGroups = [],
                    CutList = [cutListItem],
                    Operations = [],
                    EdgeBandingRequirements = [],
                    Readiness = new ManufacturingReadinessResult
                    {
                        IsReady = true,
                        Blockers = []
                    }
                }
            },
            InstallResult = new InstallPlanResult
            {
                Plan = new InstallPlan
                {
                    Steps = [],
                    Dependencies = [],
                    FasteningRequirements = [],
                    Readiness = new InstallReadinessResult
                    {
                        IsReady = true,
                        Blockers = []
                    }
                }
            }
        };
    }

    private static PersistedProjectState CreatePackagingState()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-21T18:00:00Z", CultureInfo.InvariantCulture);
        var projectId = new ProjectId(Guid.Parse("e0000000-0000-0000-0000-000000000001"));
        var revisionId = new RevisionId(Guid.Parse("e0000000-0000-0000-0000-000000000002"));
        var project = new ProjectRecord(projectId, "Invalid Design", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, createdAt, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint("invalid-design", projectId, revisionId, createdAt, null, true);
        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private static string LocateRepoSubdirectory(string subdirectory)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, subdirectory);
            if (Directory.Exists(candidate)
                && Directory.Exists(Path.Combine(current.FullName, "src"))
                && Directory.Exists(Path.Combine(current.FullName, "tests")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not locate '{subdirectory}' relative to {AppContext.BaseDirectory}.");
    }

    private sealed record WorkflowOutputs(
        byte[] PartResultBytes,
        byte[] ManufacturingPlanBytes,
        CutListExportResult Export,
        string PackagingHash);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class FixedStateSource(PersistedProjectState state) : IWorkingRevisionSource
    {
        public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null) => state;
    }

    private sealed record RebuildCurrentProjectCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.System, "V1 acceptance rebuild", []);

        public string CommandType => "v1.acceptance.rebuild";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
