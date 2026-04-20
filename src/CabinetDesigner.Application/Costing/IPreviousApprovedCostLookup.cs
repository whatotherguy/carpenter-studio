namespace CabinetDesigner.Application.Costing;

public interface IPreviousApprovedCostLookup
{
    decimal? GetMostRecentApprovedTotal();
}

public sealed class NullPreviousApprovedCostLookup : IPreviousApprovedCostLookup
{
    public decimal? GetMostRecentApprovedTotal() => null;
}
