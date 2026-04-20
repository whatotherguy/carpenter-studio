using System.Security.Cryptography;
using CabinetDesigner.Application.Packaging;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.State;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class PackagingStage : IResolutionStage
{
    private readonly IWorkingRevisionSource _workingRevisionSource;
    private readonly IClock _clock;
    private readonly IPackagingResultStore? _resultStore;
    private readonly IAppLogger? _logger;

    public PackagingStage()
        : this(
            new CurrentWorkingRevisionSource(new InMemoryDesignStateStore()),
            new SystemClock(),
            null,
            null)
    {
    }

    public PackagingStage(
        IWorkingRevisionSource workingRevisionSource,
        IClock clock,
        IPackagingResultStore? resultStore = null,
        IAppLogger? logger = null)
    {
        _workingRevisionSource = workingRevisionSource ?? throw new ArgumentNullException(nameof(workingRevisionSource));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _resultStore = resultStore;
        _logger = logger;
    }

    public int StageNumber => 11;

    public string StageName => "Packaging";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PersistedProjectState state;
        try
        {
            state = _workingRevisionSource.CaptureCurrentState(context.PartResult);
        }
        catch (Exception exception) when (exception is InvalidOperationException or PipelineStageNotExecutedException)
        {
            return Fail("PACKAGING_STATE_MISSING", "Packaging requires current persisted project state and completed upstream stage results.");
        }

        if (!context.ValidationResult.Result.IsValid)
        {
            return Fail("PACKAGING_INVALID_DESIGN", "Packaging cannot proceed while validation issues remain unresolved.");
        }

        if (context.PartResult.Parts.Count == 0 ||
            context.ManufacturingResult.Plan.CutList.Count == 0 ||
            context.CostingResult.Total <= 0m)
        {
            return Fail("PACKAGING_REQUIRED_STATE_MISSING", "Packaging requires non-empty parts, manufacturing output, and costing data.");
        }

        var createdAt = _clock.Now;
        var revisionId = state.Revision.Id;

        var designBlob = DeterministicJson.Serialize(new
        {
            schema_version = 1,
            revision_id = revisionId.Value,
            project = new
            {
                id = state.Project.Id.Value,
                name = state.Project.Name,
                state = state.Project.CurrentState.ToString()
            },
            revision = new
            {
                id = state.Revision.Id.Value,
                number = state.Revision.RevisionNumber,
                state = state.Revision.State.ToString(),
                label = state.Revision.Label,
                checkpoint_is_clean = state.Checkpoint?.IsClean
            },
            working_design = new
            {
                rooms = state.WorkingRevision.Rooms,
                walls = state.WorkingRevision.Walls,
                runs = state.WorkingRevision.Runs,
                cabinets = state.WorkingRevision.Cabinets
            }
        });
        var partsBlob = DeterministicJson.Serialize(new
        {
            schema_version = 1,
            revision_id = revisionId.Value,
            parts = context.PartResult.Parts
        });
        var manufacturingBlob = DeterministicJson.Serialize(new
        {
            schema_version = 1,
            revision_id = revisionId.Value,
            constraints = context.ConstraintResult,
            manufacturing_plan = context.ManufacturingResult.Plan
        });
        var installBlob = DeterministicJson.Serialize(new
        {
            schema_version = 1,
            revision_id = revisionId.Value,
            install_plan = context.InstallResult.Plan
        });
        var estimateBlob = DeterministicJson.Serialize(new
        {
            schema_version = 1,
            revision_id = revisionId.Value,
            costing = context.CostingResult
        });
        var validationBlob = DeterministicJson.Serialize(new
        {
            schema_version = 1,
            revision_id = revisionId.Value,
            validation = context.ValidationResult.Result
        });
        var explanationBlob = DeterministicJson.Serialize(new
        {
            schema_version = 1,
            revision_id = revisionId.Value,
            explanation_node_ids = context.ExplanationNodeIds
                .OrderBy(id => id.Value)
                .Select(id => id.Value)
                .ToArray()
        });

        var hashBytes = SHA256.HashData(
        [
            ..DeterministicJson.SerializeToUtf8Bytes(new { blob = partsBlob }),
            ..DeterministicJson.SerializeToUtf8Bytes(new { blob = manufacturingBlob }),
            ..DeterministicJson.SerializeToUtf8Bytes(new { blob = installBlob }),
            ..DeterministicJson.SerializeToUtf8Bytes(new { blob = estimateBlob }),
            ..DeterministicJson.SerializeToUtf8Bytes(new { blob = validationBlob })
        ]);
        var contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var packagingResult = new PackagingResult
        {
            SnapshotId = $"snap:{revisionId.Value:D}:{contentHash[..16]}",
            RevisionId = revisionId,
            CreatedAt = createdAt,
            ContentHash = contentHash,
            Summary = new StageResults.SnapshotSummary(
                CabinetCount: context.SpatialResult.Placements.Select(placement => placement.CabinetId).Distinct().Count(),
                RunCount: context.SpatialResult.RunSummaries.Count,
                PartCount: context.ManufacturingResult.Plan.CutList.Count,
                ValidationIssueCount: context.ValidationResult.Result.AllBaseIssues.Count,
                TotalCost: context.CostingResult.Total),
            DesignBlob = designBlob,
            PartsBlob = partsBlob,
            ManufacturingBlob = manufacturingBlob,
            InstallBlob = installBlob,
            EstimateBlob = estimateBlob,
            ValidationBlob = validationBlob,
            ExplanationBlob = explanationBlob
        };

        context.PackagingResult = packagingResult;
        _resultStore?.Update(packagingResult);

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "PackagingStage",
            Message = $"Packaged revision {revisionId} with content hash {contentHash}.",
            Timestamp = createdAt,
            StageNumber = StageNumber.ToString(),
            Properties = new Dictionary<string, string>
            {
                ["snapshotId"] = packagingResult.SnapshotId,
                ["contentHash"] = packagingResult.ContentHash
            }
        });

        return StageResult.Succeeded(StageNumber);
    }

    private StageResult Fail(string code, string message)
    {
        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Warning,
            Category = "PackagingStage",
            Message = message,
            Timestamp = _clock.Now,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.Failed(
            StageNumber,
            [new ValidationIssue(ValidationSeverity.Error, code, message)]);
    }
}
