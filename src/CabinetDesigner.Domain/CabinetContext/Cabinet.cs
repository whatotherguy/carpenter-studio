using System;
using System.Collections.Generic;
using System.Linq;
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
    public CabinetCategory Category { get; private set; }
    public ConstructionMethod Construction { get; private set; }
    public Length NominalWidth { get; private set; }
    public Length Depth { get; private set; }
    public Length Height { get; private set; }
    public int DefaultOpeningCount { get; }
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
        Length height,
        int defaultOpeningCount = 0,
        IReadOnlyList<CabinetOpening>? openings = null)
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
        DefaultOpeningCount = Math.Max(0, defaultOpeningCount);

        if (openings is not null)
        {
            foreach (var opening in openings.OrderBy(opening => opening.Index))
            {
                _openings.Add(opening);
            }
        }
    }

    public void Resize(Length newWidth, Length newDepth, Length newHeight)
    {
        if (newWidth <= Length.Zero)
        {
            throw new InvalidOperationException("Width must be positive.");
        }

        if (newDepth <= Length.Zero)
        {
            throw new InvalidOperationException("Depth must be positive.");
        }

        if (newHeight <= Length.Zero)
        {
            throw new InvalidOperationException("Height must be positive.");
        }

        NominalWidth = newWidth;
        Depth = newDepth;
        Height = newHeight;
    }

    public void Resize(Length newWidth) => Resize(newWidth, Depth, Height);

    public void SetCategory(CabinetCategory category)
    {
        Category = category;
    }

    public void SetConstruction(ConstructionMethod construction)
    {
        Construction = construction;
    }

    public CabinetOpening AddOpening(Length width, Length height, OpeningType type, int? insertIndex = null)
    {
        var index = insertIndex is null ? _openings.Count : Math.Clamp(insertIndex.Value, 0, _openings.Count);
        var opening = new CabinetOpening(OpeningId.New(), Id, width, height, type, index);
        _openings.Insert(index, opening);
        ReindexOpenings();
        return opening;
    }

    public void RemoveOpening(OpeningId openingId)
    {
        var opening = _openings.FirstOrDefault(candidate => candidate.Id == openingId)
            ?? throw new InvalidOperationException("Cabinet opening was not found.");

        _openings.Remove(opening);
        ReindexOpenings();
    }

    public void ReorderOpening(OpeningId openingId, int newIndex)
    {
        if (newIndex < 0 || newIndex >= _openings.Count)
        {
            throw new InvalidOperationException("Cabinet opening index is out of range.");
        }

        var opening = _openings.FirstOrDefault(candidate => candidate.Id == openingId)
            ?? throw new InvalidOperationException("Cabinet opening was not found.");

        _openings.Remove(opening);
        _openings.Insert(newIndex, opening);
        ReindexOpenings();
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

    private void ReindexOpenings()
    {
        for (var index = 0; index < _openings.Count; index++)
        {
            _openings[index].SetIndex(index);
        }
    }
}
