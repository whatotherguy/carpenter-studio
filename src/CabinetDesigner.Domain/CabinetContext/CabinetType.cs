using System;
using System.Collections.Generic;
using System.Linq;
using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Domain.CabinetContext;

public sealed record CabinetType
{
    public string TypeId { get; init; }
    public string Name { get; init; }
    public CabinetCategory Category { get; init; }
    public ConstructionMethod Construction { get; init; }
    public IReadOnlyList<Length> AvailableWidths { get; init; }
    public Length DefaultDepth { get; init; }
    public Length DefaultHeight { get; init; }
    public int DefaultOpeningCount { get; init; }
    public bool AllowsBelowOpening { get; init; }

    public CabinetType(
        string typeId,
        string name,
        CabinetCategory category,
        ConstructionMethod construction,
        IReadOnlyList<Length> availableWidths,
        Length defaultDepth,
        Length defaultHeight,
        int defaultOpeningCount,
        bool allowsBelowOpening = false)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            throw new InvalidOperationException("Cabinet type identifier is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Cabinet type name is required.");
        if (availableWidths.Count == 0)
            throw new InvalidOperationException("Cabinet type must define at least one width.");
        if (defaultDepth <= Length.Zero)
            throw new InvalidOperationException("Cabinet type default depth must be positive.");
        if (defaultHeight <= Length.Zero)
            throw new InvalidOperationException("Cabinet type default height must be positive.");
        if (defaultOpeningCount < 0)
            throw new InvalidOperationException("Cabinet type opening count cannot be negative.");

        TypeId = typeId;
        Name = name;
        Category = category;
        Construction = construction;
        AvailableWidths = availableWidths;
        DefaultDepth = defaultDepth;
        DefaultHeight = defaultHeight;
        DefaultOpeningCount = defaultOpeningCount;
        AllowsBelowOpening = allowsBelowOpening;
    }

    public bool Equals(CabinetType? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return TypeId == other.TypeId
            && Name == other.Name
            && Category == other.Category
            && Construction == other.Construction
            && AvailableWidths.SequenceEqual(other.AvailableWidths)
            && DefaultDepth == other.DefaultDepth
            && DefaultHeight == other.DefaultHeight
            && DefaultOpeningCount == other.DefaultOpeningCount
            && AllowsBelowOpening == other.AllowsBelowOpening;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TypeId);
        hash.Add(Name);
        hash.Add(Category);
        hash.Add(Construction);

        foreach (var width in AvailableWidths)
            hash.Add(width);

        hash.Add(DefaultDepth);
        hash.Add(DefaultHeight);
        hash.Add(DefaultOpeningCount);
        hash.Add(AllowsBelowOpening);
        return hash.ToHashCode();
    }
}
