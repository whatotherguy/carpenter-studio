using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.Export;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using System.IO;

namespace CabinetDesigner.Application.Services;

public sealed class CutListExportWorkflowService : ICutListExportWorkflowService
{
    private readonly IDesignStateStore _stateStore;
    private readonly ICatalogService _catalogService;
    private readonly ICostingPolicy _costingPolicy;
    private readonly IPreviousApprovedCostLookup _previousCostLookup;
    private readonly IValidationResultStore _validationResultStore;
    private readonly IPackagingResultStore _packagingResultStore;
    private readonly IWorkingRevisionSource _workingRevisionSource;
    private readonly ICurrentPersistedProjectState _currentPersistedProjectState;
    private readonly IClock _clock;
    private readonly IApplicationEventBus _eventBus;
    private readonly ICutListExporter _exporter;
    private readonly CabinetDesigner.Application.Diagnostics.IAppLogger? _logger;

    public CutListExportWorkflowService(
        IDesignStateStore stateStore,
        ICatalogService catalogService,
        ICostingPolicy costingPolicy,
        IPreviousApprovedCostLookup previousCostLookup,
        IValidationResultStore validationResultStore,
        IPackagingResultStore packagingResultStore,
        IWorkingRevisionSource workingRevisionSource,
        ICurrentPersistedProjectState currentPersistedProjectState,
        IClock clock,
        IApplicationEventBus eventBus,
        ICutListExporter exporter,
        CabinetDesigner.Application.Diagnostics.IAppLogger? logger = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _costingPolicy = costingPolicy ?? throw new ArgumentNullException(nameof(costingPolicy));
        _previousCostLookup = previousCostLookup ?? throw new ArgumentNullException(nameof(previousCostLookup));
        _validationResultStore = validationResultStore ?? throw new ArgumentNullException(nameof(validationResultStore));
        _packagingResultStore = packagingResultStore ?? throw new ArgumentNullException(nameof(packagingResultStore));
        _workingRevisionSource = workingRevisionSource ?? throw new ArgumentNullException(nameof(workingRevisionSource));
        _currentPersistedProjectState = currentPersistedProjectState ?? throw new ArgumentNullException(nameof(currentPersistedProjectState));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _logger = logger;
    }

    public CutListWorkflowResult BuildCurrentProjectCutList()
    {
        var state = _currentPersistedProjectState.CurrentState
            ?? throw new InvalidOperationException("No project state is currently loaded.");
        var validationStageExecuted = false;
        var context = new ResolutionContext
        {
            Command = new RebuildCurrentProjectCommand(),
            Mode = ResolutionMode.Full
        };

        foreach (var stage in BuildStages())
        {
            var result = stage.Execute(context);
            context.AccumulatedIssues.AddRange(result.Issues);
            context.ExplanationNodeIds.AddRange(result.ExplanationNodeIds);
            validationStageExecuted |= stage.StageNumber == 10;

            if (!result.Success || result.IsNotImplemented || context.HasBlockingIssues)
            {
                if (!validationStageExecuted)
                {
                    _validationResultStore.Update(new FullValidationResult
                    {
                        CrossCuttingIssues = [],
                        ContextualIssues = context.AccumulatedIssues
                            .OrderByDescending(issue => issue.Severity)
                            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                            .ToArray()
                    });
                }

                PublishRefresh();
                var failureMessage = context.AccumulatedIssues.LastOrDefault()?.Message
                    ?? $"Stage {stage.StageNumber} '{stage.StageName}' failed.";
                return new CutListWorkflowResult(false, null, null, failureMessage);
            }
        }

        var summary = new ProjectSummary(
            state.Project.Name,
            state.Revision.Label ?? $"Rev {state.Revision.RevisionNumber}",
            state.Revision.CreatedAt.ToUniversalTime(),
            "Carpenter Studio");
        var export = _exporter.Export(new CutListExportRequest(
            context.ManufacturingResult.Plan,
            summary,
            context.ConstraintResult.MaterialAssignments));

        PublishRefresh();
        return new CutListWorkflowResult(true, export, BuildFileStem(summary.ProjectName, summary.RevisionLabel), null);
    }

    private IReadOnlyList<IResolutionStage> BuildStages() =>
    [
        new InputCaptureStage(_stateStore),
        new InteractionInterpretationStage(new InMemoryDeltaTracker(), _stateStore, _catalogService),
        new SpatialResolutionStage(_stateStore),
        new EngineeringResolutionStage(_stateStore),
        new PartGenerationStage(_stateStore, _logger),
        new ConstraintPropagationStage(_catalogService, _stateStore),
        new ManufacturingPlanningStage(),
        new InstallPlanningStage(),
        new CostingStage(_catalogService, _costingPolicy, _logger, _previousCostLookup),
        new ValidationStage(_validationResultStore, _currentPersistedProjectState),
        new PackagingStage(_workingRevisionSource, _clock, _packagingResultStore, _logger)
    ];

    private void PublishRefresh() =>
        _eventBus.Publish(new CabinetDesigner.Application.Events.DesignChangedEvent(
            CabinetDesigner.Application.DTOs.CommandResultDto.NoOp("cut_list.export")));

    private static string BuildFileStem(string projectName, string revisionLabel) =>
        $"{SanitizeFileNameSegment(projectName)}-{SanitizeFileNameSegment(revisionLabel)}";

    private static string SanitizeFileNameSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = value
            .Select(character => invalid.Contains(character) ? '-' : character)
            .ToArray();
        return new string(buffer).Trim().Replace(' ', '-');
    }

    private sealed record RebuildCurrentProjectCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.System, "Rebuild current project for cut list export", []);

        public string CommandType => "cut_list.export";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
