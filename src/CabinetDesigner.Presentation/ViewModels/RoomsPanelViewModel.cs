using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Presentation.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using System.Threading;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class RoomsPanelViewModel : ObservableObject, IDisposable
{
    private readonly IRoomService _roomService;
    private readonly IApplicationEventBus _eventBus;
    private readonly Action<Guid?> _setActiveRoom;
    private IReadOnlyList<RoomListItemViewModel> _rooms = [];
    private RoomListItemViewModel? _selectedRoom;
    private Guid? _activeRoomId;
    private string _pendingRoomName = "New Room";
    private string _pendingCeilingHeightText = "96";
    private bool _isBusy;

    public RoomsPanelViewModel(
        IRoomService roomService,
        IApplicationEventBus eventBus,
        IAppLogger logger,
        Action<Guid?> setActiveRoom)
    {
        _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _setActiveRoom = setActiveRoom ?? throw new ArgumentNullException(nameof(setActiveRoom));
        ArgumentNullException.ThrowIfNull(logger);

        AddRoomCommand = new AsyncRelayCommand(AddRoomAsync, logger, eventBus, () => !string.IsNullOrWhiteSpace(PendingRoomName));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, logger, eventBus);

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<ActiveRoomChangedEvent>(OnActiveRoomChanged);
    }

    public IReadOnlyList<RoomListItemViewModel> Rooms
    {
        get => _rooms;
        private set => SetProperty(ref _rooms, value);
    }

    public RoomListItemViewModel? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (SetProperty(ref _selectedRoom, value))
            {
                _activeRoomId = value?.RoomId.Value;
                SyncSelectedRoom();
            }
        }
    }

    public string PendingRoomName
    {
        get => _pendingRoomName;
        set
        {
            if (SetProperty(ref _pendingRoomName, value))
            {
                AddRoomCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string PendingCeilingHeightText
    {
        get => _pendingCeilingHeightText;
        set => SetProperty(ref _pendingCeilingHeightText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool HasRooms => Rooms.Count > 0;

    public string EmptyStateText => HasRooms
        ? "Select a room to make it active."
        : "Create a room to begin the design session.";

    public AsyncRelayCommand AddRoomCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public async Task InitializeAsync() => await RefreshAsync().ConfigureAwait(true);

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<ActiveRoomChangedEvent>(OnActiveRoomChanged);
    }

    private async Task AddRoomAsync()
    {
        if (!TryParseCeilingHeight(PendingCeilingHeightText, out var ceilingHeight))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var room = await _roomService.CreateRoomAsync(PendingRoomName, ceilingHeight, CancellationToken.None).ConfigureAwait(true);
            PendingRoomName = "New Room";
            await RefreshAsync().ConfigureAwait(true);
            SelectedRoom = Rooms.FirstOrDefault(roomItem => roomItem.RoomId == room.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var rooms = await _roomService.ListRoomsAsync(CancellationToken.None).ConfigureAwait(true);
            var selectedRoomId = _activeRoomId;
            Rooms = rooms
                .OrderBy(room => room.Id.Value)
                .Select(room => new RoomListItemViewModel(
                    room.Id,
                    room.Name,
                    FormatLength(room.CeilingHeight),
                    $"{room.Walls.Count} walls"))
                .ToArray();

            SelectedRoom = Rooms.FirstOrDefault(room => selectedRoomId is not null && room.RoomId.Value == selectedRoomId.Value);
            if (SelectedRoom is null && Rooms.Count > 0)
            {
                SelectedRoom = Rooms[0];
            }

            OnPropertyChanged(nameof(HasRooms));
            OnPropertyChanged(nameof(EmptyStateText));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SyncSelectedRoom()
    {
        foreach (var room in Rooms)
        {
            room.IsSelected = SelectedRoom?.RoomId == room.RoomId;
        }

        _activeRoomId = SelectedRoom?.RoomId.Value;
        _setActiveRoom(SelectedRoom?.RoomId.Value);
        _eventBus.Publish(new ActiveRoomChangedEvent(SelectedRoom?.RoomId.Value));
    }

    private void OnProjectOpened(ProjectOpenedEvent @event) =>
        DispatchIfNeeded(() =>
        {
            _ = RefreshAsync();
        });

    private void OnProjectClosed(ProjectClosedEvent _) =>
        DispatchIfNeeded(() =>
        {
            Rooms = [];
            SelectedRoom = null;
            OnPropertyChanged(nameof(HasRooms));
            OnPropertyChanged(nameof(EmptyStateText));
        });

    private void OnActiveRoomChanged(ActiveRoomChangedEvent @event) =>
        DispatchIfNeeded(() =>
        {
            if (@event.RoomId is null)
            {
                SelectedRoom = null;
                return;
            }

            _activeRoomId = @event.RoomId;
            SelectedRoom = Rooms.FirstOrDefault(room => room.RoomId.Value == @event.RoomId.Value);
        });

    private static bool TryParseCeilingHeight(string? text, out Length ceilingHeight)
    {
        var normalized = (text ?? string.Empty).Trim().TrimEnd('"');
        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var inches) && inches > 0m)
        {
            ceilingHeight = Length.FromInches(inches);
            return true;
        }

        ceilingHeight = Length.Zero;
        return false;
    }

    private static string FormatLength(Length length) => $"{length.Inches:0.##}\"";

    private static void DispatchIfNeeded(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
