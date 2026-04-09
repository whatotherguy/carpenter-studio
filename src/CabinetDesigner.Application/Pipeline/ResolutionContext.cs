using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline;

public sealed class ResolutionContext
{
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
        get => _inputCapture ?? throw new InvalidOperationException("Stage 1 (Input Capture) has not executed.");
        set => _inputCapture = value;
    }

    public InteractionInterpretationResult Interpretation
    {
        get => _interpretation ?? throw new InvalidOperationException("Stage 2 (Interaction Interpretation) has not executed.");
        set => _interpretation = value;
    }

    public SpatialResolutionResult SpatialResult
    {
        get => _spatialResult ?? throw new InvalidOperationException("Stage 3 (Spatial Resolution) has not executed.");
        set => _spatialResult = value;
    }

    public EngineeringResolutionResult EngineeringResult
    {
        get => _engineeringResult ?? throw new InvalidOperationException("Stage 4 (Engineering Resolution) has not executed.");
        set => _engineeringResult = value;
    }

    public ConstraintPropagationResult ConstraintResult
    {
        get => _constraintResult ?? throw new InvalidOperationException("Stage 5 (Constraint Propagation) has not executed.");
        set => _constraintResult = value;
    }

    public PartGenerationResult PartResult
    {
        get => _partResult ?? throw new InvalidOperationException("Stage 6 (Part Generation) has not executed.");
        set => _partResult = value;
    }

    public ManufacturingPlanResult ManufacturingResult
    {
        get => _manufacturingResult ?? throw new InvalidOperationException("Stage 7 (Manufacturing Planning) has not executed.");
        set => _manufacturingResult = value;
    }

    public InstallPlanResult InstallResult
    {
        get => _installResult ?? throw new InvalidOperationException("Stage 8 (Install Planning) has not executed.");
        set => _installResult = value;
    }

    public CostingResult CostingResult
    {
        get => _costingResult ?? throw new InvalidOperationException("Stage 9 (Costing) has not executed.");
        set => _costingResult = value;
    }

    public ValidationResult ValidationResult
    {
        get => _validationResult ?? throw new InvalidOperationException("Stage 10 (Validation) has not executed.");
        set => _validationResult = value;
    }

    public PackagingResult PackagingResult
    {
        get => _packagingResult ?? throw new InvalidOperationException("Stage 11 (Packaging) has not executed.");
        set => _packagingResult = value;
    }

    public List<ValidationIssue> AccumulatedIssues { get; } = [];

    public List<ExplanationNodeId> ExplanationNodeIds { get; } = [];

    public bool HasBlockingIssues => AccumulatedIssues.Any(issue => issue.Severity >= ValidationSeverity.Error);
}
