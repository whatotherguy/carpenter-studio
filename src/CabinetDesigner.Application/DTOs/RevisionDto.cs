namespace CabinetDesigner.Application.DTOs;

public sealed record RevisionDto(
    Guid RevisionId,
    string Label,
    DateTimeOffset CreatedAt,
    string ApprovalState,
    bool IsApproved,
    bool IsLocked);
