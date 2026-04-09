namespace CabinetDesigner.Presentation.ViewModels;

public sealed record RunSlotViewModel(
    Guid CabinetId,
    string CabinetTypeId,
    string WidthDisplay,
    int Index,
    bool IsSelected);
