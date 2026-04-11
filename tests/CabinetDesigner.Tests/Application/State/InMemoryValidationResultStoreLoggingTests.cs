using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Application.State;

public sealed class InMemoryValidationResultStoreLoggingTests
{
    [Fact]
    public void Update_WhenResultHasManufactureBlockers_LogsWarning()
    {
        var logger = new CapturingLogger();
        var store = new InMemoryValidationResultStore(logger);
        var result = BuildResultWithBlocker();

        store.Update(result);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Validation", entry.Category);
        Assert.NotNull(entry.Properties);
        Assert.True(int.Parse(entry.Properties!["manufactureBlockers"]) > 0);
    }

    [Fact]
    public void Update_WhenResultHasNoManufactureBlockers_DoesNotLog()
    {
        var logger = new CapturingLogger();
        var store = new InMemoryValidationResultStore(logger);
        var result = BuildResultWithWarningOnly();

        store.Update(result);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Update_WithNoLogger_DoesNotThrow_WhenBlockersPresent()
    {
        var store = new InMemoryValidationResultStore();
        var result = BuildResultWithBlocker();

        var ex = Record.Exception(() => store.Update(result));

        Assert.Null(ex);
    }

    private static FullValidationResult BuildResultWithBlocker() =>
        new()
        {
            ContextualIssues = [],
            CrossCuttingIssues =
            [
                new ExtendedValidationIssue
                {
                    IssueId = new ValidationIssueId("mfg.blocked", ["run-1"]),
                    Issue = new ValidationIssue(ValidationSeverity.ManufactureBlocker, "mfg.blocked", "Cannot manufacture.", ["run-1"]),
                    RuleCode = "mfg.blocked",
                    Category = ValidationRuleCategory.RunIntegrity,
                    Scope = ValidationRuleScope.Run,
                    SuggestedFixes = []
                }
            ]
        };

    private static FullValidationResult BuildResultWithWarningOnly() =>
        new()
        {
            ContextualIssues = [new ValidationIssue(ValidationSeverity.Warning, "warn.001", "Width near limit")],
            CrossCuttingIssues = []
        };

    private sealed class CapturingLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }
}
