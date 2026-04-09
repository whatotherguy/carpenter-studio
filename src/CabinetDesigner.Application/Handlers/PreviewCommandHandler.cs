using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Handlers;

public sealed class PreviewCommandHandler : IPreviewCommandHandler
{
    private readonly IResolutionOrchestrator _orchestrator;

    public PreviewCommandHandler(IResolutionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public PreviewResultDto Preview(IDesignCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var structureIssues = command.ValidateStructure();
        if (structureIssues.Any(issue => issue.Severity >= ValidationSeverity.Error))
        {
            return PreviewResultDto.Invalid("Structural validation failed.");
        }

        var result = _orchestrator.Preview(command);
        return PreviewResultDto.From(result);
    }
}
