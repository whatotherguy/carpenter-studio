using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class RoomListItemViewModel : ObservableObject
{
    private bool _isSelected;

    public RoomListItemViewModel(RoomId roomId, string name, string ceilingHeightDisplay, string wallCountDisplay)
    {
        RoomId = roomId;
        Name = name;
        CeilingHeightDisplay = ceilingHeightDisplay;
        WallCountDisplay = wallCountDisplay;
    }

    public RoomId RoomId { get; }

    public string Name { get; }

    public string CeilingHeightDisplay { get; }

    public string WallCountDisplay { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
