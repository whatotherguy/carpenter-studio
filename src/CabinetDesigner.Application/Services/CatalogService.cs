using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.TemplateContext;

namespace CabinetDesigner.Application.Services;

public sealed class CatalogService : ICatalogService
{
    private static readonly IReadOnlyList<CatalogItemDto> Items = BuildCatalog();

    public IReadOnlyList<CatalogItemDto> GetAllItems() => Items;

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

    private static Guid StableIdFor(string cabinetTypeId)
    {
        var guidBytes = MD5.HashData(Encoding.UTF8.GetBytes(cabinetTypeId));
        return new Guid(guidBytes);
    }
}
