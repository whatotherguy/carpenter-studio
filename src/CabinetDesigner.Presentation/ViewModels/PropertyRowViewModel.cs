namespace CabinetDesigner.Presentation.ViewModels;

public sealed record PropertyRowViewModel(
    string Key,
    string DisplayName,
    string DisplayValue,
    bool IsEditable = false,
    bool IsPlaceholder = false)
{
    public string EditabilityDisplay => IsEditable ? "Editable" : "Read-only";
}
