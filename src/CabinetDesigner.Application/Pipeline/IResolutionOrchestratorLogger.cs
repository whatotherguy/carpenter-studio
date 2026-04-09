using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline;

public interface IResolutionOrchestratorLogger
{
    void LogUnhandledException(IDesignCommand command, ResolutionMode mode, Exception exception);
}
