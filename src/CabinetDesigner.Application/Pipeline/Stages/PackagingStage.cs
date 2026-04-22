using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.Packaging;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class PackagingStage : IResolutionStage
{
    private const string StateMissingCode = "PACKAGING_STATE_MISSING";
    private const string InvalidDesignCode = "PACKAGING_INVALID_DESIGN";
    private const string RequiredStateMissingCode = "PACKAGING_REQUIRED_STATE_MISSING";

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
            return Fail(StateMissingCode, "Packaging requires current persisted project state and completed upstream stage results.");
        }

        if (!context.ValidationResult.Result.IsValid)
        {
            return Fail(InvalidDesignCode, "Packaging cannot proceed while validation issues remain unresolved.");
        }

        if (context.PartResult.Parts.Count == 0 ||
            context.ManufacturingResult.Plan.CutList.Count == 0)
        {
            return Fail(RequiredStateMissingCode, "Packaging requires non-empty parts and manufacturing output.");
        }

        var createdAt = _clock.Now;
        var revisionId = state.Revision.Id;

        var partsBytes = DeterministicJson.Serialize(new
        {
            revision_id = revisionId.Value,
            parts = context.PartResult.Parts
        });
        var manufacturingPlanBytes = DeterministicJson.Serialize(new
        {
            revision_id = revisionId.Value,
            manufacturing_plan = context.ManufacturingResult.Plan
        });
        var installPlanBytes = DeterministicJson.Serialize(new
        {
            revision_id = revisionId.Value,
            install_plan = context.InstallResult.Plan
        });
        var constraintAssignmentsBytes = DeterministicJson.Serialize(new
        {
            revision_id = revisionId.Value,
            constraint_assignments = context.ConstraintResult
        });
        var validationSummaryBytes = DeterministicJson.Serialize(new
        {
            revision_id = revisionId.Value,
            validation_summary = new
            {
                is_valid = context.ValidationResult.Result.IsValid,
                issue_count = context.ValidationResult.Result.AllBaseIssues.Count,
                issues = context.ValidationResult.Result.AllBaseIssues
            }
        });
        // V2: Swap this canonical no-pricing snapshot for vendor-priced costing details. See docs/V2_enhancements.md.
        var costingBytes = context.CostingResult.Status == CostingStatus.NotConfigured
            ? DeterministicJson.Serialize(new
            {
                revision_id = revisionId.Value,
                status = "not_configured"
            })
            : DeterministicJson.Serialize(new
            {
                revision_id = revisionId.Value,
                status = context.CostingResult.Status,
                status_reason = context.CostingResult.StatusReason,
                material_cost = context.CostingResult.MaterialCost,
                hardware_cost = context.CostingResult.HardwareCost,
                labor_cost = context.CostingResult.LaborCost,
                install_cost = context.CostingResult.InstallCost,
                subtotal = context.CostingResult.Subtotal,
                markup = context.CostingResult.Markup,
                tax = context.CostingResult.Tax,
                total = context.CostingResult.Total,
                revision_delta = context.CostingResult.RevisionDelta,
                cabinet_breakdowns = context.CostingResult.CabinetBreakdowns
            });
        var workingDesignBytes = DeterministicJson.Serialize(new
        {
            revision_id = revisionId.Value,
            working_design = new
            {
                rooms = state.WorkingRevision.Rooms,
                walls = state.WorkingRevision.Walls,
                runs = state.WorkingRevision.Runs,
                cabinets = state.WorkingRevision.Cabinets
            },
            explanation_node_ids = context.ExplanationNodeIds
                .Select(id => id.Value)
                .OrderBy(id => id)
                .ToArray(),
            checkpoint_is_clean = state.Checkpoint?.IsClean
        });

        var hashBytes = SHA256.HashData(
        [
            ..partsBytes,
            ..manufacturingPlanBytes,
            ..installPlanBytes,
            ..constraintAssignmentsBytes,
            ..validationSummaryBytes,
            ..costingBytes
        ]);
        var contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var designBlob = Encoding.UTF8.GetString(constraintAssignmentsBytes);
        var partsBlob = Encoding.UTF8.GetString(partsBytes);
        var manufacturingBlob = Encoding.UTF8.GetString(manufacturingPlanBytes);
        var installBlob = Encoding.UTF8.GetString(installPlanBytes);
        var estimateBlob = Encoding.UTF8.GetString(costingBytes);
        var validationBlob = Encoding.UTF8.GetString(validationSummaryBytes);
        var explanationBlob = Encoding.UTF8.GetString(workingDesignBytes);

        var packagingResult = new PackagingResult
        {
            SnapshotId = $"snap:{revisionId.Value:D}:{contentHash[..16]}",
            RevisionId = revisionId,
            CreatedAt = createdAt,
            ContentHash = contentHash,
            Summary = new StageResults.SnapshotSummary(
                CabinetCount: context.SpatialResult.Placements.Select(placement => placement.CabinetId).Distinct().Count(),
                RunCount: context.SpatialResult.RunSummaries.Count,
                PartCount: context.PartResult.Parts.Count,
                ValidationIssueCount: context.ValidationResult.Result.AllBaseIssues.Count,
                CostingStatus: context.CostingResult.Status),
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
