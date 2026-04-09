using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Persistence;

public interface IWorkingRevisionSource
{
    PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null);
}
