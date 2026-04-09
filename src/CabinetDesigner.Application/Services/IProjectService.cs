using System.Threading;
using System.Threading.Tasks;

namespace CabinetDesigner.Application.Services;

public interface IProjectService
{
    Task<ProjectSummaryDto> OpenProjectAsync(string filePath, CancellationToken ct = default);

    Task<ProjectSummaryDto> CreateProjectAsync(string name, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);

    Task<RevisionDto> SaveRevisionAsync(string label, CancellationToken ct = default);

    Task CloseAsync();

    ProjectSummaryDto? CurrentProject { get; }
}
