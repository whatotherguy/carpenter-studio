using System.Threading;
using System.Threading.Tasks;

namespace CabinetDesigner.Application.Services;

public interface ISnapshotService
{
    Task<RevisionDto> ApproveRevisionAsync(string label, CancellationToken ct = default);

    Task<RevisionDto> LoadSnapshotAsync(Guid revisionId, CancellationToken ct = default);

    Task<IReadOnlyList<RevisionDto>> GetRevisionHistoryAsync(CancellationToken ct = default);
}
