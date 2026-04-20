using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Costing;

public sealed class DefaultCostingPolicy : ICostingPolicy
{
    public decimal GetLaborRate(ManufacturingOperationKind kind) =>
        kind switch
        {
            ManufacturingOperationKind.SawCutRectangle => 0m,
            ManufacturingOperationKind.ApplyEdgeBanding => 0m,
            _ => 0m
        };

    public decimal InstallRatePerStep => 0m;

    public decimal MarkupFraction => 0m;

    public decimal TaxFraction => 0m;
}
