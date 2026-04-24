using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Presentation.Commands;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class AlphaLimitationsDialogViewModel : ObservableObject
{
    private readonly Action? _closeAction;

    public AlphaLimitationsDialogViewModel()
        : this(null, AlphaLimitations.All)
    {
    }

    public AlphaLimitationsDialogViewModel(Action? closeAction, IReadOnlyList<AlphaLimitation>? limitations = null)
    {
        _closeAction = closeAction;

        var source = limitations ?? AlphaLimitations.All;
        Groups = source
            .OrderBy(limitation => limitation.Area)
            .ThenBy(limitation => limitation.Code, StringComparer.Ordinal)
            .GroupBy(limitation => limitation.Area)
            .Select(group => new AlphaLimitationGroupViewModel(
                group.Key,
                group.OrderBy(limitation => limitation.Code, StringComparer.Ordinal).ToArray()))
            .ToArray();

        CloseCommand = new RelayCommand(() => _closeAction?.Invoke());
    }

    public IReadOnlyList<AlphaLimitationGroupViewModel> Groups { get; }

    public RelayCommand CloseCommand { get; }
}

public sealed record AlphaLimitationGroupViewModel(
    AlphaArea Area,
    IReadOnlyList<AlphaLimitation> Limitations)
{
    public string AreaLabel => Area.ToString();
}
