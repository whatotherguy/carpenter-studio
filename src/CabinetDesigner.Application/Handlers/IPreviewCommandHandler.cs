using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Handlers;

public interface IPreviewCommandHandler
{
    PreviewResultDto Preview(IDesignCommand command);
}
