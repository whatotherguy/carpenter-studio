using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Persistence;

public interface ICommandJournalRepository
{
    Task AppendAsync(CommandJournalEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<CommandJournalEntry>> LoadForRevisionAsync(
        RevisionId revisionId,
        CancellationToken ct = default);
}
