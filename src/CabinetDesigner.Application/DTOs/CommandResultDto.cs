using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.DTOs;

public sealed record CommandResultDto(
    Guid CommandId,
    string CommandType,
    bool Success,
    IReadOnlyList<ValidationIssueSummaryDto> Issues,
    IReadOnlyList<string> AffectedEntityIds,
    IReadOnlyList<string> ExplanationNodeIds)
{
    public static CommandResultDto From(CommandResult result, string commandType)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);

        return new CommandResultDto(
            result.CommandMetadata.CommandId.Value,
            commandType,
            result.Success,
            result.Issues.Select(ValidationIssueSummaryDto.From).ToArray(),
            result.CommandMetadata.AffectedEntityIds,
            result.ExplanationNodeIds.Select(nodeId => nodeId.Value.ToString()).ToArray());
    }

    public static CommandResultDto Rejected(
        CommandMetadata metadata,
        string commandType,
        IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        ArgumentNullException.ThrowIfNull(issues);

        return new CommandResultDto(
            metadata.CommandId.Value,
            commandType,
            false,
            issues.Select(ValidationIssueSummaryDto.From).ToArray(),
            metadata.AffectedEntityIds,
            []);
    }

    public static CommandResultDto NoOp(string commandType) =>
        new(Guid.Empty, commandType, false, [], [], []);
}
