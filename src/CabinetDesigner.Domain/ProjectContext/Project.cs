using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.ProjectContext;

public sealed class Project
{
    private readonly List<Revision> _revisions = [];

    public ProjectId Id { get; }
    public string Name { get; private set; }
    public string? CustomerName { get; private set; }
    public string? JobSiteAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastModifiedAt { get; private set; }
    public IReadOnlyList<Revision> Revisions => _revisions;
    public Revision? CurrentRevision => _revisions.MaxBy(r => r.VersionNumber);

    public Project(ProjectId id, string name, DateTimeOffset createdAt)
    {
        if (id == default)
            throw new InvalidOperationException("Project must have an identifier.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Project name is required.");

        Id = id;
        Name = name;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;

        _revisions.Add(Revision.CreateDraft(Id, 1, createdAt));
    }

    public void UpdateName(string name, DateTimeOffset modifiedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Project name is required.");

        Name = name;
        Touch(modifiedAt);
    }

    public Revision CreateNewRevision(DateTimeOffset createdAt)
    {
        var revision = Revision.CreateDraft(Id, _revisions.Count + 1, createdAt);
        _revisions.Add(revision);
        Touch(createdAt);
        return revision;
    }

    private void Touch(DateTimeOffset modifiedAt) => LastModifiedAt = modifiedAt;
}
