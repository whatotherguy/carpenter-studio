using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Costing;

public interface ICostingPolicy
{
    decimal GetLaborRate(ManufacturingOperationKind kind);

    decimal InstallRatePerStep { get; }

    decimal MarkupFraction { get; }

    decimal TaxFraction { get; }
}
