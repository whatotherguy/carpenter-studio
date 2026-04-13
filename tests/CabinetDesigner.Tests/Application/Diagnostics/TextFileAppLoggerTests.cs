using CabinetDesigner.Application.Diagnostics;
using Xunit;

namespace CabinetDesigner.Tests.Application.Diagnostics;

public sealed class TextFileAppLoggerTests
{
    [Fact]
    public async Task Log_WithConcurrentCalls_WritesAllEntriesWithoutCorruption()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"TextFileAppLoggerTests-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var logger = new TextFileAppLogger(tempDir, mirrorToDebug: false);
            const int taskCount = 10;
            const int logsPerTask = 100;
            const int expectedTotal = taskCount * logsPerTask;

            var tasks = Enumerable.Range(0, taskCount)
                .Select(taskIndex => Task.Run(() =>
                {
                    for (int i = 0; i < logsPerTask; i++)
                    {
                        var entry = new LogEntry
                        {
                            Level = LogLevel.Info,
                            Category = $"Task{taskIndex}",
                            Message = $"Log entry {i} from task {taskIndex}",
                            Timestamp = DateTimeOffset.UtcNow
                        };

                        logger.Log(entry);
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            var logFiles = Directory.GetFiles(tempDir, "app-*.log");
            Assert.NotEmpty(logFiles);

            var allLines = new List<string>();
            foreach (var logFile in logFiles)
            {
                var lines = File.ReadAllLines(logFile);
                allLines.AddRange(lines);
            }

            Assert.Equal(expectedTotal, allLines.Count);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
