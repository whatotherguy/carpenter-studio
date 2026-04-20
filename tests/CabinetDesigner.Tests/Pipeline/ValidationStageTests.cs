using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class ValidationStageTests
{
    [Fact]
    public void Execute_WorkflowSnapshot_ReflectsCheckpointDirtyFlag()
    {
        var capturedContext = (ValidationContext?)null;
        var engine = new CapturingValidationEngine(ctx => capturedContext = ctx);
        var projectState = new StubProjectState(
            CreateProjectState(isClean: false, revisionState: ApprovalState.Draft));
        var stage = new ValidationStage(engine, projectState: projectState);
        var context = BuildContext(withManufacturingBlockers: false);

        stage.Execute(context);

        Assert.NotNull(capturedContext);
        Assert.True(capturedContext.WorkflowState.HasUnapprovedChanges);
    }

    [Fact]
    public void Execute_WorkflowSnapshot_CleanCheckpointProducesNoUnapprovedChanges()
    {
        var capturedContext = (ValidationContext?)null;
        var engine = new CapturingValidationEngine(ctx => capturedContext = ctx);
        var projectState = new StubProjectState(
            CreateProjectState(isClean: true, revisionState: ApprovalState.Draft));
        var stage = new ValidationStage(engine, projectState: projectState);
        var context = BuildContext(withManufacturingBlockers: false);

        stage.Execute(context);

        Assert.NotNull(capturedContext);
        Assert.False(capturedContext.WorkflowState.HasUnapprovedChanges);
    }

    [Fact]
    public void Execute_WorkflowSnapshot_ReflectsManufacturingBlockers()
    {
        var capturedContext = (ValidationContext?)null;
        var engine = new CapturingValidationEngine(ctx => capturedContext = ctx);
        var projectState = new StubProjectState(
            CreateProjectState(isClean: true, revisionState: ApprovalState.Draft));
        var stage = new ValidationStage(engine, projectState: projectState);
        var context = BuildContext(withManufacturingBlockers: true);

        stage.Execute(context);

        Assert.NotNull(capturedContext);
        Assert.True(capturedContext.WorkflowState.HasPendingManufactureBlockers);
    }

    [Fact]
    public void Execute_WorkflowSnapshot_ApprovalState_ReflectsRevisionState()
    {
        var capturedContext = (ValidationContext?)null;
        var engine = new CapturingValidationEngine(ctx => capturedContext = ctx);
        var projectState = new StubProjectState(
            CreateProjectState(isClean: true, revisionState: ApprovalState.UnderReview));
        var stage = new ValidationStage(engine, projectState: projectState);
        var context = BuildContext(withManufacturingBlockers: false);

        stage.Execute(context);

        Assert.NotNull(capturedContext);
        Assert.Equal("UnderReview", capturedContext.WorkflowState.ApprovalState);
    }

    [Fact]
    public void Execute_WorkflowSnapshot_DefaultsToDraft_WhenNoProjectState()
    {
        var capturedContext = (ValidationContext?)null;
        var engine = new CapturingValidationEngine(ctx => capturedContext = ctx);
        var stage = new ValidationStage(engine, projectState: null);
        var context = BuildContext(withManufacturingBlockers: false);

        stage.Execute(context);

        Assert.NotNull(capturedContext);
        Assert.Equal("Draft", capturedContext.WorkflowState.ApprovalState);
        Assert.False(capturedContext.WorkflowState.HasUnapprovedChanges);
    }

    private static ResolutionContext BuildContext(bool withManufacturingBlockers)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.SpatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [],
            Placements = []
        };

        context.EngineeringResult = new EngineeringResolutionResult
        {
            Assemblies = [],
            FillerRequirements = [],
            EndConditionUpdates = []
        };

        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };

        var blockers = withManufacturingBlockers
            ? new[]
            {
                new ManufacturingBlocker
                {
                    Code = ManufacturingBlockerCode.NoPartsProduced,
                    Message = "No parts produced.",
                    AffectedEntityIds = []
                }
            }
            : [];

        context.ManufacturingResult = new ManufacturingPlanResult
        {
            Plan = new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = [],
                Operations = [],
                EdgeBandingRequirements = [],
                Readiness = new ManufacturingReadinessResult
                {
                    IsReady = !withManufacturingBlockers,
                    Blockers = blockers
                }
            }
        };

        context.InstallResult = new InstallPlanResult
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
        };

        return context;
    }

    private static PersistedProjectState CreateProjectState(bool isClean, ApprovalState revisionState)
    {
        var projectId = new ProjectId(Guid.Parse("80000000-0000-0000-0000-000000000001"));
        var revisionId = new RevisionId(Guid.Parse("70000000-0000-0000-0000-000000000001"));

        return new PersistedProjectState(
            Project: new ProjectRecord(
                projectId,
                "Test Project",
                null,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                ApprovalState.Draft),
            Revision: new RevisionRecord(
                revisionId,
                projectId,
                1,
                revisionState,
                DateTimeOffset.UnixEpoch,
                null,
                null,
                "Rev 1"),
            WorkingRevision: new WorkingRevision(
                new RevisionRecord(
                    revisionId,
                    projectId,
                    1,
                    revisionState,
                    DateTimeOffset.UnixEpoch,
                    null,
                    null,
                    "Rev 1"),
                [],
                [],
                [],
                [],
                []),
            Checkpoint: new AutosaveCheckpoint(
                Guid.NewGuid().ToString(),
                projectId,
                revisionId,
                DateTimeOffset.UtcNow,
                null,
                isClean));
    }

    private sealed class StubProjectState : ICurrentPersistedProjectState
    {
        private readonly PersistedProjectState _state;

        public StubProjectState(PersistedProjectState state) => _state = state;

        public PersistedProjectState? CurrentState => _state;

        public void SetCurrentState(PersistedProjectState state) { }

        public void Clear() { }
    }

    private sealed class CapturingValidationEngine : IValidationEngine
    {
        private readonly Action<ValidationContext>? _onValidate;

        public CapturingValidationEngine(Action<ValidationContext>? onValidate = null)
        {
            _onValidate = onValidate;
        }

        public ValidationContext? CapturedContext { get; private set; }

        public IReadOnlyList<IValidationRule> RegisteredRules => [];

        public FullValidationResult Validate(ValidationContext context, IReadOnlyList<ValidationIssue>? contextualIssues = null)
        {
            CapturedContext = context;
            _onValidate?.Invoke(context);
            return new FullValidationResult
            {
                CrossCuttingIssues = [],
                ContextualIssues = contextualIssues ?? []
            };
        }

        public IReadOnlyList<ValidationIssue> ValidatePreview(ValidationContext context) => [];

        public IReadOnlyList<ExtendedValidationIssue> ValidateCategory(ValidationContext context, ValidationRuleCategory category) => [];
    }

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Validation Stage Test", []);

        public string CommandType => "test.validation_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
