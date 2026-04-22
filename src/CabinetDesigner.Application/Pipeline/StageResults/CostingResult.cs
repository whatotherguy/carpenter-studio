namespace CabinetDesigner.Application.Pipeline.StageResults;

public enum CostingStatus
{
    NotConfigured,
    Calculated,
    Failed
}

public sealed record CostingResult
{
    public CostingStatus Status { get; init; } = CostingStatus.NotConfigured;

    public string? StatusReason { get; init; }

    public required decimal MaterialCost { get; init; }

    public required decimal HardwareCost { get; init; }

    public required decimal LaborCost { get; init; }

    public required decimal InstallCost { get; init; }

    public required decimal Subtotal { get; init; }

    public required decimal Markup { get; init; }

    public required decimal Tax { get; init; }

    public required decimal Total { get; init; }

    public CostDelta? RevisionDelta { get; init; }

    public required IReadOnlyList<CabinetCostBreakdown> CabinetBreakdowns { get; init; }
}

public sealed record CostDelta(
    decimal PreviousTotal,
    decimal CurrentTotal,
    decimal Difference,
    string Summary);

public sealed record CabinetCostBreakdown(
    string CabinetId,
    decimal MaterialCost,
    decimal HardwareCost,
    decimal LaborCost,
    decimal InstallCost)
{
    public decimal Subtotal => MaterialCost + HardwareCost + LaborCost + InstallCost;
}
