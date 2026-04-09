using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.CabinetContext;

public sealed class Cabinet
{
    private readonly Dictionary<string, OverrideValue> _overrides = [];
    private readonly List<CabinetOpening> _openings = [];

    public CabinetId Id { get; }
    public RevisionId RevisionId { get; }
    public string CabinetTypeId { get; }
    public CabinetCategory Category { get; }
    public ConstructionMethod Construction { get; }
    public Length NominalWidth { get; private set; }
    public Length Depth { get; private set; }
    public Length Height { get; private set; }
    public IReadOnlyDictionary<string, OverrideValue> Overrides => _overrides;
    public IReadOnlyList<CabinetOpening> Openings => _openings;

    public Cabinet(
        CabinetId id,
        RevisionId revisionId,
        string cabinetTypeId,
        CabinetCategory category,
        ConstructionMethod construction,
        Length nominalWidth,
        Length depth,
        Length height)
    {
        if (id == default)
            throw new InvalidOperationException("Cabinet must have an identifier.");
        if (revisionId == default)
            throw new InvalidOperationException("Cabinet must belong to a revision.");
        if (string.IsNullOrWhiteSpace(cabinetTypeId))
            throw new InvalidOperationException("Cabinet type identifier is required.");
        if (nominalWidth <= Length.Zero)
            throw new InvalidOperationException("Cabinet width must be positive.");
        if (depth <= Length.Zero)
            throw new InvalidOperationException("Cabinet depth must be positive.");
        if (height <= Length.Zero)
            throw new InvalidOperationException("Cabinet height must be positive.");

        Id = id;
        RevisionId = revisionId;
        CabinetTypeId = cabinetTypeId;
        Category = category;
        Construction = construction;
        NominalWidth = nominalWidth;
        Depth = depth;
        Height = height;
    }

    public void Resize(Length newWidth)
    {
        if (newWidth <= Length.Zero)
            throw new InvalidOperationException("Width must be positive.");

        NominalWidth = newWidth;
    }

    public CabinetOpening AddOpening(Length width, Length height, OpeningType type)
    {
        var opening = new CabinetOpening(OpeningId.New(), Id, width, height, type, _openings.Count);
        _openings.Add(opening);
        return opening;
    }

    public void SetOverride(string key, OverrideValue value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Override key is required.");

        _overrides[key] = value;
    }

    public void RemoveOverride(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Override key is required.");

        _overrides.Remove(key);
    }
}
