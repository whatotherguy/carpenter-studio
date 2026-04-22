using CabinetDesigner.Application.Export;

namespace CabinetDesigner.Application.Services;

public interface ICutListExportWorkflowService
{
    CutListWorkflowResult BuildCurrentProjectCutList();
}
