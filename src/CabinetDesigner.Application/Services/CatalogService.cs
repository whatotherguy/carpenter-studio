using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.HardwareContext;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;
using CabinetDesigner.Domain.TemplateContext;

namespace CabinetDesigner.Application.Services;

public sealed class CatalogService : ICatalogService
{
    private static readonly IReadOnlyList<CatalogItemDto> Items = BuildCatalog();
    private static readonly IReadOnlyDictionary<string, MaterialRecord> Materials = BuildMaterials();
    private static readonly IReadOnlyDictionary<Guid, MaterialRecord> MaterialsById = Materials.Values
        .OrderBy(material => material.Key, StringComparer.Ordinal)
        .ToDictionary(material => material.Id.Value, material => material);
    private static readonly IReadOnlyDictionary<CabinetCategory, IReadOnlyList<HardwareRecord>> HardwareByCategory = BuildHardware();
    private static readonly IReadOnlyDictionary<Guid, HardwareRecord> HardwareById = HardwareByCategory.Values
        .SelectMany(records => records)
        .DistinctBy(record => record.Id.Value)
        .OrderBy(record => record.Key, StringComparer.Ordinal)
        .ToDictionary(record => record.Id.Value, record => record);

    public IReadOnlyList<CatalogItemDto> GetAllItems() => Items;

    public MaterialId ResolvePartMaterial(string partType, CabinetCategory category, ConstructionMethod construction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partType);

        var materialKey = partType switch
        {
            "Back" => "material:back-panel-quarter",
            "AdjustableShelf" => "material:shelf-melamine-three-quarter",
            "FrameStile" or "FrameRail" or "FrameMullion" when construction == ConstructionMethod.FaceFrame
                => "material:face-frame-hardwood-three-quarter",
            "LeftSide" or "RightSide" or "Top" or "Bottom" or "ToeKick" or "StructuralBase"
                => category == CabinetCategory.Wall
                    ? "material:wall-case-plywood-three-quarter"
                    : "material:case-plywood-three-quarter",
            _ => string.Empty
        };

        return materialKey.Length == 0 || !Materials.TryGetValue(materialKey, out var material)
            ? default
            : material.Id;
    }

    public Thickness ResolvePartThickness(string partType, CabinetCategory category)
    {
        var materialKey = partType switch
        {
            "Back" => "material:back-panel-quarter",
            "AdjustableShelf" => "material:shelf-melamine-three-quarter",
            "FrameStile" or "FrameRail" or "FrameMullion" => "material:face-frame-hardwood-three-quarter",
            "LeftSide" or "RightSide" or "Top" or "Bottom" or "ToeKick" or "StructuralBase"
                => category == CabinetCategory.Wall
                    ? "material:wall-case-plywood-three-quarter"
                    : "material:case-plywood-three-quarter",
            _ => string.Empty
        };

        return materialKey.Length == 0 || !Materials.TryGetValue(materialKey, out var material)
            ? Thickness.Exact(Length.Zero)
            : material.Thickness;
    }

    public bool IsKnownMaterial(MaterialId id) =>
        id != default && MaterialsById.ContainsKey(id.Value);

    public Thickness ResolveMaterialThickness(MaterialId id) =>
        MaterialsById.TryGetValue(id.Value, out var material)
            ? material.Thickness
            : Thickness.Exact(Length.Zero);

    public GrainDirection ResolveMaterialGrain(MaterialId id) =>
        MaterialsById.TryGetValue(id.Value, out var material)
            ? material.GrainDirection
            : GrainDirection.None;

    public IReadOnlyList<HardwareItemId> ResolveHardwareForOpening(OpeningId openingId, CabinetCategory category)
    {
        _ = openingId;
        return HardwareByCategory.TryGetValue(category, out var hardware)
            ? hardware
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => item.Id)
                .ToArray()
            : [];
    }

    public decimal GetMaterialPricePerSquareFoot(MaterialId id, Thickness thickness)
    {
        if (!MaterialsById.TryGetValue(id.Value, out var material))
        {
            return 0m;
        }

        return material.Thickness == thickness
            ? material.PricePerSquareFoot
            : 0m;
    }

    public decimal GetHardwarePrice(HardwareItemId id) =>
        HardwareById.TryGetValue(id.Value, out var hardware)
            ? hardware.Price
            : 0m;

    private static IReadOnlyList<CatalogItemDto> BuildCatalog()
    {
        var templates = new[]
        {
            CreateTemplate("base-24", "Base Cabinet 24\"", Length.FromInches(24m), Length.FromInches(24m), Length.FromInches(34.5m)),
            CreateTemplate("base-30", "Base Cabinet 30\"", Length.FromInches(30m), Length.FromInches(24m), Length.FromInches(34.5m)),
            CreateTemplate("base-36", "Base Cabinet 36\"", Length.FromInches(36m), Length.FromInches(24m), Length.FromInches(34.5m)),
            CreateTemplate("wall-30", "Wall Cabinet 30\"", Length.FromInches(30m), Length.FromInches(12m), Length.FromInches(30m)),
            CreateTemplate("wall-36", "Wall Cabinet 36\"", Length.FromInches(36m), Length.FromInches(12m), Length.FromInches(30m)),
            CreateTemplate("tall-24", "Tall Cabinet 24\"", Length.FromInches(24m), Length.FromInches(24m), Length.FromInches(84m)),
            CreateTemplate("tall-36", "Tall Cabinet 36\"", Length.FromInches(36m), Length.FromInches(24m), Length.FromInches(84m))
        };

        return templates.Select(Map).ToArray();
    }

    private static CabinetTemplate CreateTemplate(
        string cabinetTypeId,
        string name,
        Length width,
        Length depth,
        Length height) =>
        new(
            new TemplateId(StableIdFor(cabinetTypeId)),
            name,
            cabinetTypeId,
            width,
            depth,
            height,
            new Dictionary<string, OverrideValue>());

    private static CatalogItemDto Map(CabinetTemplate template)
    {
        var category = InferCategory(template.CabinetTypeId);
        var description = $"{category} cabinet template. {template.DefaultWidth.Inches:0.##}\" W x {template.DefaultDepth.Inches:0.##}\" D x {template.DefaultHeight.Inches:0.##}\" H.";

        return new CatalogItemDto(
            template.CabinetTypeId,
            template.Name,
            category,
            description,
            template.DefaultWidth.Inches);
    }

    private static string InferCategory(string cabinetTypeId) =>
        cabinetTypeId.StartsWith("base-", StringComparison.OrdinalIgnoreCase) ? "Base"
        : cabinetTypeId.StartsWith("wall-", StringComparison.OrdinalIgnoreCase) ? "Wall"
        : cabinetTypeId.StartsWith("tall-", StringComparison.OrdinalIgnoreCase) ? "Tall"
        : "Cabinet";

    internal static Guid StableIdFor(string cabinetTypeId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cabinetTypeId));
        return new Guid(hashBytes.AsSpan(0, 16));
    }

    private static IReadOnlyDictionary<string, MaterialRecord> BuildMaterials()
    {
        var materials = new[]
        {
            new MaterialRecord(
                "material:case-plywood-three-quarter",
                "Shop Birch Case Plywood",
                MaterialCategory.Plywood,
                Thickness.Exact(Length.FromInches(0.75m)),
                GrainDirection.LengthWise,
                4.85m),
            new MaterialRecord(
                "material:wall-case-plywood-three-quarter",
                "Wall Cabinet Birch Plywood",
                MaterialCategory.Plywood,
                Thickness.Exact(Length.FromInches(0.75m)),
                GrainDirection.LengthWise,
                4.95m),
            new MaterialRecord(
                "material:shelf-melamine-three-quarter",
                "White Melamine Shelf Stock",
                MaterialCategory.Melamine,
                Thickness.Exact(Length.FromInches(0.75m)),
                GrainDirection.None,
                3.25m),
            new MaterialRecord(
                "material:back-panel-quarter",
                "Back Panel Stock",
                MaterialCategory.MDF,
                Thickness.Exact(Length.FromInches(0.25m)),
                GrainDirection.None,
                1.8m),
            new MaterialRecord(
                "material:face-frame-hardwood-three-quarter",
                "Face Frame Hardwood",
                MaterialCategory.HardwoodSolid,
                Thickness.Exact(Length.FromInches(0.75m)),
                GrainDirection.LengthWise,
                6.5m)
        };

        return materials.ToDictionary(material => material.Key, material => material, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<CabinetCategory, IReadOnlyList<HardwareRecord>> BuildHardware()
    {
        var hinge = new HardwareRecord(
            "hardware:hinge-110-soft-close",
            HardwareCategory.Hinge,
            9.5m,
            new BoringPattern(Length.FromInches(1.75m), Length.FromInches(0.875m), 2));
        var pull = new HardwareRecord(
            "hardware:pull-128mm-brushed",
            HardwareCategory.Pull,
            4.25m,
            null);

        return new Dictionary<CabinetCategory, IReadOnlyList<HardwareRecord>>
        {
            [CabinetCategory.Base] = [hinge, pull],
            [CabinetCategory.Wall] = [hinge, pull],
            [CabinetCategory.Tall] = [hinge, pull],
            [CabinetCategory.Vanity] = [hinge, pull]
        };
    }

    private sealed record MaterialRecord(
        string Key,
        string Name,
        MaterialCategory Category,
        Thickness Thickness,
        GrainDirection GrainDirection,
        decimal PricePerSquareFoot)
    {
        public MaterialId Id { get; } = new(StableIdFor(Key));
    }

    private sealed record HardwareRecord(
        string Key,
        HardwareCategory Category,
        decimal Price,
        BoringPattern? BoringPattern)
    {
        public HardwareItemId Id { get; } = new(StableIdFor(Key));
    }
}
