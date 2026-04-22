using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Export;

public sealed record ProjectSummary(
    string ProjectName,
    string RevisionLabel,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedBy);

public sealed record CutListExportRequest(
    ManufacturingPlan Plan,
    ProjectSummary Summary,
    IReadOnlyList<MaterialAssignment> Materials);
