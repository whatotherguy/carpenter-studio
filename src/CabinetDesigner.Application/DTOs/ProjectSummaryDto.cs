namespace CabinetDesigner.Application.DTOs;

public sealed record ProjectSummaryDto(
    Guid ProjectId,
    string Name,
    string FilePath,
    DateTimeOffset LastModified,
    string CurrentRevisionLabel,
    bool HasUnsavedChanges);
