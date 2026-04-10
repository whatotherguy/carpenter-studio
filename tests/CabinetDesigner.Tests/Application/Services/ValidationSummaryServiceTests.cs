using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Application.Services;

public sealed class ValidationSummaryServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static ValidationSummaryService BuildService(IValidationResultStore store) =>
        new(store);

    private static InMemoryValidationResultStore EmptyStore() =>
        new();

    private static InMemoryValidationResultStore StoreWith(FullValidationResult result)
    {
        var store = new InMemoryValidationResultStore();
        store.Update(result);
        return store;
    }

    private static FullValidationResult BuildResult(
        IReadOnlyList<ValidationIssue>? contextual = null,
        IReadOnlyList<ExtendedValidationIssue>? crossCutting = null) =>
        new()
        {
            ContextualIssues = contextual ?? [],
            CrossCuttingIssues = crossCutting ?? []
        };

    private static ValidationIssue MakeIssue(
        ValidationSeverity severity,
        string code,
        string message,
        IReadOnlyList<string>? entityIds = null) =>
        new(severity, code, message, entityIds);

    private static ExtendedValidationIssue MakeExtended(
        ValidationSeverity severity,
        string code,
        string message,
        IReadOnlyList<string> entityIds) =>
        new()
        {
            IssueId = new ValidationIssueId(code, entityIds),
            Issue = MakeIssue(severity, code, message, entityIds),
            RuleCode = code,
            Category = ValidationRuleCategory.RunIntegrity,
            Scope = ValidationRuleScope.Run,
            SuggestedFixes = []
        };

    // ── no-validation-run / empty store ──────────────────────────────────────

    [Fact]
    public void GetAllIssues_WhenNoValidationHasRun_ReturnsEmptyList()
    {
        var service = BuildService(EmptyStore());

        var issues = service.GetAllIssues();

        Assert.Empty(issues);
    }

    [Fact]
    public void GetIssuesFor_WhenNoValidationHasRun_ReturnsEmptyList()
    {
        var service = BuildService(EmptyStore());

        var issues = service.GetIssuesFor("any-entity");

        Assert.Empty(issues);
    }

    [Fact]
    public void HasManufactureBlockers_WhenNoValidationHasRun_ReturnsFalse()
    {
        var service = BuildService(EmptyStore());

        Assert.False(service.HasManufactureBlockers);
    }

    // ── no issues in result ──────────────────────────────────────────────────

    [Fact]
    public void GetAllIssues_WhenResultHasNoIssues_ReturnsEmptyList()
    {
        var service = BuildService(StoreWith(BuildResult()));

        Assert.Empty(service.GetAllIssues());
    }

    [Fact]
    public void HasManufactureBlockers_WhenNoBlockerIssues_ReturnsFalse()
    {
        var result = BuildResult(
            contextual: [MakeIssue(ValidationSeverity.Warning, "warn.001", "Just a warning")]);
        var service = BuildService(StoreWith(result));

        Assert.False(service.HasManufactureBlockers);
    }

    // ── one or more issues present ────────────────────────────────────────────

    [Fact]
    public void GetAllIssues_WhenResultHasContextualIssues_ReturnsMappedDtos()
    {
        var result = BuildResult(
            contextual:
            [
                MakeIssue(ValidationSeverity.Warning, "warn.001", "Width near limit"),
                MakeIssue(ValidationSeverity.Error, "err.001", "Over capacity")
            ]);
        var service = BuildService(StoreWith(result));

        var issues = service.GetAllIssues();

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Code == "warn.001" && i.Severity == "Warning");
        Assert.Contains(issues, i => i.Code == "err.001" && i.Severity == "Error");
    }

    [Fact]
    public void GetAllIssues_WhenResultHasCrossCuttingIssues_ReturnsMappedDtos()
    {
        var result = BuildResult(
            crossCutting:
            [
                MakeExtended(ValidationSeverity.Error, "run.over_capacity", "Run exceeds capacity.", ["run-1"])
            ]);
        var service = BuildService(StoreWith(result));

        var issues = service.GetAllIssues();

        var issue = Assert.Single(issues);
        Assert.Equal("run.over_capacity", issue.Code);
        Assert.Equal("Error", issue.Severity);
        Assert.Contains("run-1", issue.AffectedEntityIds!);
    }

    [Fact]
    public void GetAllIssues_CombinesContextualAndCrossCuttingIssues()
    {
        var result = BuildResult(
            contextual: [MakeIssue(ValidationSeverity.Info, "ctx.info", "Info")],
            crossCutting: [MakeExtended(ValidationSeverity.Warning, "cc.warn", "Cross-cut warning", [])]);
        var service = BuildService(StoreWith(result));

        var issues = service.GetAllIssues();

        Assert.Equal(2, issues.Count);
    }

    [Fact]
    public void HasManufactureBlockers_WhenBlockerIssuePresent_ReturnsTrue()
    {
        var result = BuildResult(
            crossCutting:
            [
                MakeExtended(ValidationSeverity.ManufactureBlocker, "mfg.blocked", "Cannot manufacture.", ["run-2"])
            ]);
        var service = BuildService(StoreWith(result));

        Assert.True(service.HasManufactureBlockers);
    }

    // ── GetIssuesFor entity filtering ─────────────────────────────────────────

    [Fact]
    public void GetIssuesFor_ReturnsOnlyIssuesAffectingThatEntity()
    {
        var result = BuildResult(
            crossCutting:
            [
                MakeExtended(ValidationSeverity.Error, "run.over_capacity", "Run-1 issue", ["run-1"]),
                MakeExtended(ValidationSeverity.Warning, "run.filler", "Run-2 issue", ["run-2"])
            ]);
        var service = BuildService(StoreWith(result));

        var issues = service.GetIssuesFor("run-1");

        var issue = Assert.Single(issues);
        Assert.Equal("run.over_capacity", issue.Code);
    }

    [Fact]
    public void GetIssuesFor_WhenEntityHasNoIssues_ReturnsEmpty()
    {
        var result = BuildResult(
            crossCutting:
            [
                MakeExtended(ValidationSeverity.Error, "run.over_capacity", "Run-1 issue", ["run-1"])
            ]);
        var service = BuildService(StoreWith(result));

        var issues = service.GetIssuesFor("run-99");

        Assert.Empty(issues);
    }

    [Fact]
    public void GetIssuesFor_ContextualIssuesWithNoEntityIds_AreExcluded()
    {
        var result = BuildResult(
            contextual: [MakeIssue(ValidationSeverity.Warning, "ctx.warn", "Global warning", null)]);
        var service = BuildService(StoreWith(result));

        var issues = service.GetIssuesFor("some-entity");

        Assert.Empty(issues);
    }

    [Fact]
    public void GetIssuesFor_NullEntityId_Throws()
    {
        var service = BuildService(EmptyStore());

        Assert.Throws<ArgumentNullException>(() => service.GetIssuesFor(null!));
    }

    // ── store update replaces previous result ─────────────────────────────────

    [Fact]
    public void GetAllIssues_AfterStoreUpdate_ReflectsLatestResult()
    {
        var store = new InMemoryValidationResultStore();
        store.Update(BuildResult(contextual: [MakeIssue(ValidationSeverity.Warning, "old.warn", "Old")]));
        var service = BuildService(store);

        // first run: one warning
        Assert.Single(service.GetAllIssues());

        // pipeline runs again with a clean result
        store.Update(BuildResult());

        Assert.Empty(service.GetAllIssues());
    }

    // ── InMemoryValidationResultStore.Update null guard ───────────────────────

    [Fact]
    public void InMemoryValidationResultStore_UpdateWithNull_Throws()
    {
        var store = new InMemoryValidationResultStore();

        Assert.Throws<ArgumentNullException>(() => store.Update(null!));
    }
}
