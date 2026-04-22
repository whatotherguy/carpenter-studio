namespace CabinetDesigner.Application.Export;

public sealed record CutListExportResult(
    byte[] Csv,
    byte[] Txt,
    byte[] Html,
    string ContentHash);
