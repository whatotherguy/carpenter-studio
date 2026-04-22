namespace CabinetDesigner.Application.Events;

public sealed record DesignChangedEvent(CommandResultDto Result) : IApplicationEvent;

public sealed record ProjectOpenedEvent(ProjectSummaryDto Project) : IApplicationEvent;

public sealed record ProjectClosedEvent(Guid ProjectId) : IApplicationEvent;

public sealed record ActiveRoomChangedEvent(Guid? RoomId) : IApplicationEvent;

public sealed record RevisionApprovedEvent(RevisionDto Revision) : IApplicationEvent;

public sealed record UndoAppliedEvent(CommandResultDto Result) : IApplicationEvent;

public sealed record RedoAppliedEvent(CommandResultDto Result) : IApplicationEvent;

public sealed record CommandExecutionFailedEvent(string Message, Exception Exception) : IApplicationEvent;
