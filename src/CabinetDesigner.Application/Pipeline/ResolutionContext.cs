using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline;

public sealed class ResolutionContext
{
    private readonly Dictionary<int, string> _skippedStages = [];

    private InputCaptureResult? _inputCapture;
    private InteractionInterpretationResult? _interpretation;
    private SpatialResolutionResult? _spatialResult;
    private EngineeringResolutionResult? _engineeringResult;
    private ConstraintPropagationResult? _constraintResult;
    private PartGenerationResult? _partResult;
    private ManufacturingPlanResult? _manufacturingResult;
    private InstallPlanResult? _installResult;
    private CostingResult? _costingResult;
    private ValidationResult? _validationResult;
    private PackagingResult? _packagingResult;

    public required IDesignCommand Command { get; init; }

    public required ResolutionMode Mode { get; init; }

    public InputCaptureResult InputCapture
    {
        get => _inputCapture ?? ThrowNotExecuted<InputCaptureResult>(1, "Input Capture");
        set => _inputCapture = value;
    }

    public InteractionInterpretationResult Interpretation
    {
        get => _interpretation ?? ThrowNotExecuted<InteractionInterpretationResult>(2, "Interaction Interpretation");
        set => _interpretation = value;
    }

    public SpatialResolutionResult SpatialResult
    {
        get => _spatialResult ?? ThrowNotExecuted<SpatialResolutionResult>(3, "Spatial Resolution");
        set => _spatialResult = value;
    }

    public EngineeringResolutionResult EngineeringResult
    {
        get => _engineeringResult ?? ThrowNotExecuted<EngineeringResolutionResult>(4, "Engineering Resolution");
        set => _engineeringResult = value;
    }

    public ConstraintPropagationResult ConstraintResult
    {
        get => _constraintResult ?? ThrowNotExecuted<ConstraintPropagationResult>(5, "Constraint Propagation");
        set => _constraintResult = value;
    }

    public PartGenerationResult PartResult
    {
        get => _partResult ?? ThrowNotExecuted<PartGenerationResult>(6, "Part Generation");
        set => _partResult = value;
    }

    public ManufacturingPlanResult ManufacturingResult
    {
        get => _manufacturingResult ?? ThrowNotExecuted<ManufacturingPlanResult>(7, "Manufacturing Planning");
        set => _manufacturingResult = value;
    }

    public InstallPlanResult InstallResult
    {
        get => _installResult ?? ThrowNotExecuted<InstallPlanResult>(8, "Install Planning");
        set => _installResult = value;
    }

    public CostingResult CostingResult
    {
        get => _costingResult ?? ThrowNotExecuted<CostingResult>(9, "Costing");
        set => _costingResult = value;
    }

    public ValidationResult ValidationResult
    {
        get => _validationResult ?? ThrowNotExecuted<ValidationResult>(10, "Validation");
        set => _validationResult = value;
    }

    public PackagingResult PackagingResult
    {
        get => _packagingResult ?? ThrowNotExecuted<PackagingResult>(11, "Packaging");
        set => _packagingResult = value;
    }

    public List<ValidationIssue> AccumulatedIssues { get; } = [];

    public List<ExplanationNodeId> ExplanationNodeIds { get; } = [];

    public bool HasBlockingIssues => AccumulatedIssues.Any(issue => issue.Severity >= ValidationSeverity.Error);

    /// <summary>
    /// Records that <paramref name="stageName"/> (stage <paramref name="stageNumber"/>) was
    /// deliberately skipped because <see cref="IResolutionStage.ShouldExecute"/> returned
    /// <see langword="false"/> for the current pipeline mode.
    /// </summary>
    public void MarkStageSkipped(int stageNumber, string stageName) =>
        _skippedStages[stageNumber] = stageName;

    // Returns T only to satisfy the compiler's nullable-flow analysis; always throws.
    private T ThrowNotExecuted<T>(int stageNumber, string stageName)
    {
        if (_skippedStages.TryGetValue(stageNumber, out var skippedStageName))
        {
            throw PipelineStageNotExecutedException.Skipped(stageNumber, skippedStageName, Mode);
        }

        throw PipelineStageNotExecutedException.NeverInvoked(stageNumber, stageName);
    }
}
