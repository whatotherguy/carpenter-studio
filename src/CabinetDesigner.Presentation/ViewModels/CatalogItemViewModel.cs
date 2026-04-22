namespace CabinetDesigner.Presentation.ViewModels;

public sealed record CatalogItemViewModel(
    string TypeId,
    string DisplayName,
    string Category,
    string ConstructionMethod,
    string Description,
    string DimensionsDisplay,
    string DefaultOpeningsDisplay,
    string DefaultWidthDisplay,
    decimal DefaultNominalWidthInches);
