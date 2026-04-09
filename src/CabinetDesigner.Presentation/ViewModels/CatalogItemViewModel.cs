namespace CabinetDesigner.Presentation.ViewModels;

public sealed record CatalogItemViewModel(
    string TypeId,
    string DisplayName,
    string Category,
    string Description,
    string DefaultWidthDisplay,
    decimal DefaultNominalWidthInches);
