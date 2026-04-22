namespace CabinetDesigner.Application.Export;

public interface ICutListExporter
{
    CutListExportResult Export(CutListExportRequest request);
}
