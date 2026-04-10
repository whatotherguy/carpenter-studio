using System.Collections.Concurrent;
using System.Threading;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Services;

namespace CabinetDesigner.Tests.Presentation;

internal sealed class CapturingAppLogger : IAppLogger
{
    private readonly object _syncRoot = new();
    private readonly List<LogEntry> _entries = [];
    private readonly ConcurrentQueue<TaskCompletionSource<LogEntry>> _waiters = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_syncRoot)
            {
                return [.. _entries];
            }
        }
    }

    public void Log(LogEntry entry)
    {
        lock (_syncRoot)
        {
            _entries.Add(entry);
        }

        while (_waiters.TryDequeue(out var waiter))
        {
            waiter.TrySetResult(entry);
        }
    }

    /// <summary>
    /// Returns a task that completes when the next log entry is written, or faults
    /// with <see cref="TimeoutException"/> after <paramref name="timeout"/>.
    /// </summary>
    public async Task<LogEntry> WaitForEntryAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<LogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters.Enqueue(tcs);

        using var cts = new CancellationTokenSource(timeout);
        await using var registration = cts.Token.Register(
            () => tcs.TrySetException(new TimeoutException($"No log entry was written within {timeout}.")),
            useSynchronizationContext: false);

        return await tcs.Task.ConfigureAwait(false);
    }
}

internal sealed class ThrowingValidationSummaryService : IValidationSummaryService
{
    public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() => throw new NotImplementedException();

    public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) => throw new NotImplementedException();

    public bool HasManufactureBlockers => throw new NotImplementedException();
}
