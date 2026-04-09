using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class RunMapper
{
    public static RunRow ToRow(CabinetRun run, RevisionId revisionId, int runIndex, DateTimeOffset timestamp) => new()
    {
        Id = run.Id.Value.ToString(),
        RevisionId = revisionId.Value.ToString(),
        WallId = run.WallId.Value.ToString(),
        RunIndex = runIndex,
        StartOffset = LengthText.FormatLength(Length.Zero),
        EndOffset = LengthText.FormatLength(run.Capacity),
        EndConditionStart = run.LeftEndCondition.Type.ToString(),
        EndConditionEnd = run.RightEndCondition.Type.ToString(),
        CreatedAt = timestamp.UtcDateTime.ToString("O"),
        UpdatedAt = timestamp.UtcDateTime.ToString("O")
    };

    public static CabinetRun ToDomain(RunRow row)
    {
        var run = new CabinetRun(
            new RunId(Guid.Parse(row.Id)),
            new WallId(Guid.Parse(row.WallId)),
            LengthText.ParseLength(row.EndOffset));

        if (!string.IsNullOrWhiteSpace(row.EndConditionStart))
        {
            run.SetLeftEndCondition(Enum.Parse<EndConditionType>(row.EndConditionStart, ignoreCase: true));
        }

        if (!string.IsNullOrWhiteSpace(row.EndConditionEnd))
        {
            run.SetRightEndCondition(Enum.Parse<EndConditionType>(row.EndConditionEnd, ignoreCase: true));
        }

        return run;
    }
}
