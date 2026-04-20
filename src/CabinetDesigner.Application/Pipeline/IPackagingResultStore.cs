using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline;

public interface IPackagingResultStore
{
    PackagingResult? Current { get; }

    void Update(PackagingResult result);

    void Clear();
}
