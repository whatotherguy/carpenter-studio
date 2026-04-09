using System.Collections.Generic;

namespace CabinetDesigner.Domain.Commands;

public abstract record DesignCommandBase : IDesignCommand
{
    public CommandMetadata Metadata { get; }

    public abstract string CommandType { get; }

    protected DesignCommandBase(CommandMetadata metadata)
    {
        Metadata = metadata;
    }

    public abstract IReadOnlyList<ValidationIssue> ValidateStructure();
}
