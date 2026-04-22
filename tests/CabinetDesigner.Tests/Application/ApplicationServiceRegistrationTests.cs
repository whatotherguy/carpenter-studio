using System.Threading;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Application.Export;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CabinetDesigner.Tests.Application;

public sealed class ApplicationServiceRegistrationTests
{
    [Fact]
    public void AddApplicationServices_ComposesCoreInfrastructure()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICommandPersistencePort, NoOpCommandPersistencePort>();
        services.AddApplicationServices();
        services.AddSingleton<IAppLogger, RecordingAppLogger>();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IApplicationEventBus>());
        Assert.NotNull(provider.GetRequiredService<IResolutionOrchestrator>());
        Assert.NotNull(provider.GetRequiredService<IDesignCommandHandler>());
        Assert.NotNull(provider.GetRequiredService<IClock>());
        Assert.NotNull(provider.GetRequiredService<ICutListExporter>());
        Assert.IsType<RecordingAppLogger>(provider.GetRequiredService<IAppLogger>());
    }

    private sealed class NoOpCommandPersistencePort : ICommandPersistencePort
    {
        public Task CommitCommandAsync(IDesignCommand command, CommandResult result, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }
}
