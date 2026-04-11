using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Validation;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryValidationResultStore : IValidationResultStore
{
    private readonly IAppLogger? _logger;
    private volatile FullValidationResult? _current;

    public InMemoryValidationResultStore(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public FullValidationResult? Current => _current;

    public void Update(FullValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _current = result;

        var counts = result.SeverityCounts;
        if (counts.ManufactureBlockers > 0)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Warning,
                Category = "Validation",
                Message = "Validation found manufacture blockers.",
                Timestamp = DateTimeOffset.UtcNow,
                Properties = new Dictionary<string, string>
                {
                    ["manufactureBlockers"] = counts.ManufactureBlockers.ToString(),
                    ["errors"] = counts.Errors.ToString(),
                    ["warnings"] = counts.Warnings.ToString()
                }
            });
        }
    }
}
