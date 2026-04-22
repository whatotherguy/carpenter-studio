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

    public bool IsPricingConfigured
    {
        // V2: Vendor-managed pricing tables will seed real prices here. See docs/V2_enhancements.md.
        get => false;
    }

    public IReadOnlyList<CatalogItemDto> GetAllItems() => Items;

    public MaterialId ResolvePartMaterial(string partType, CabinetCategory category, ConstructionMethod construction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partType);

        var materialKey = partType switch
        {
            "Back" or "DrawerBoxBottom" => "material:back-panel-quarter",
            "AdjustableShelf" => "material:shelf-melamine-three-quarter",
            "DrawerBoxLeftSide" or "DrawerBoxRightSide" or "DrawerBoxFront" or "DrawerBoxBack"
                => "material:drawer-box-half",
            "FrameStile" or "FrameRail" or "FrameMullion" when construction == ConstructionMethod.FaceFrame
                => "material:face-frame-hardwood-three-quarter",
            "LeftSide" or "RightSide" or "Top" or "Bottom" or "ToeKick" or "StructuralBase"
            or "Door" or "DrawerFront"
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
            "Back" or "DrawerBoxBottom" => "material:back-panel-quarter",
            "AdjustableShelf" => "material:shelf-melamine-three-quarter",
            "DrawerBoxLeftSide" or "DrawerBoxRightSide" or "DrawerBoxFront" or "DrawerBoxBack"
                => "material:drawer-box-half",
            "FrameStile" or "FrameRail" or "FrameMullion" => "material:face-frame-hardwood-three-quarter",
            "LeftSide" or "RightSide" or "Top" or "Bottom" or "ToeKick" or "StructuralBase"
            or "Door" or "DrawerFront"
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

    public string GetMaterialDisplayName(MaterialId id) =>
        MaterialsById.TryGetValue(id.Value, out var material)
            ? material.Name
            : id == default
                ? "Unassigned"
                : id.Value.ToString("D");

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
        // V2: vendor hardware catalog will return hinges/slides per opening type (Door → hinge, Drawer → slide)
        _ = openingId;
        _ = category;
        return [];
    }

    public decimal GetMaterialPricePerSquareFoot(MaterialId id, Thickness thickness)
    {
        if (!IsPricingConfigured)
        {
            return 0m;
        }

        if (!MaterialsById.TryGetValue(id.Value, out var material))
        {
            return 0m;
        }

        return material.Thickness == thickness
            ? material.PricePerSquareFoot
            : 0m;
    }

    public decimal GetHardwarePrice(HardwareItemId id) =>
        !IsPricingConfigured
            ? 0m
            :
        HardwareById.TryGetValue(id.Value, out var hardware)
            ? hardware.Price
            : 0m;

    private static IReadOnlyList<CatalogItemDto> BuildCatalog()
    {
        var templates = new[]
        {
            new CatalogTemplate(
                "base-standard-24",
                "Base Standard 24",
                CabinetCategory.Base,
                ConstructionMethod.Frameless,
                Length.FromInches(24m),
                Length.FromInches(24m),
                Length.FromInches(34.5m),
                1),
            new CatalogTemplate(
                "base-drawer-18",
                "Base Drawer 18",
                CabinetCategory.Base,
                ConstructionMethod.Frameless,
                Length.FromInches(18m),
                Length.FromInches(24m),
                Length.FromInches(34.5m),
                3),
            new CatalogTemplate(
                "wall-standard-30",
                "Wall Standard 30",
                CabinetCategory.Wall,
                ConstructionMethod.Frameless,
                Length.FromInches(30m),
                Length.FromInches(12m),
                Length.FromInches(30m),
                2),
            new CatalogTemplate(
                "tall-pantry-24",
                "Tall Pantry 24",
                CabinetCategory.Tall,
                ConstructionMethod.Frameless,
                Length.FromInches(24m),
                Length.FromInches(24m),
                Length.FromInches(84m),
                2),
            new CatalogTemplate(
                "base-faceframe-24",
                "Base FaceFrame 24",
                CabinetCategory.Base,
                ConstructionMethod.FaceFrame,
                Length.FromInches(24m),
                Length.FromInches(24m),
                Length.FromInches(34.5m),
                1)
        };

        return templates.Select(Map).ToArray();
    }

    private static CatalogItemDto Map(CatalogTemplate template)
    {
        var description = $"{template.Category} cabinet template. {template.NominalWidth.Inches:0.##}\" W x {template.Depth.Inches:0.##}\" D x {template.Height.Inches:0.##}\" H.";

        return new CatalogItemDto(
            template.CabinetTypeId,
            template.DisplayName,
            template.Category.ToString(),
            template.ConstructionMethod,
            template.NominalWidth,
            template.Depth,
            template.Height,
            template.DefaultOpenings,
            description);
    }

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
                6.5m),
            new MaterialRecord(
                "material:drawer-box-half",
                "Drawer Box Half-Inch Baltic Birch",
                MaterialCategory.Plywood,
                Thickness.Exact(Length.FromInches(0.5m)),
                GrainDirection.LengthWise,
                0m)
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

    private sealed record CatalogTemplate(
        string CabinetTypeId,
        string DisplayName,
        CabinetCategory Category,
        ConstructionMethod ConstructionMethod,
        Length NominalWidth,
        Length Depth,
        Length Height,
        int DefaultOpenings);
}
