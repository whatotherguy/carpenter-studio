using CabinetDesigner.Application.State;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.MaterialContext;

namespace CabinetDesigner.Application.Pipeline.Parts;

public static class PartGeometry
{
    public const string FrontEdgeBandingId = "edge-banding:front";
    public const string ThicknessOverrideKey = "materialThickness";
    public const string ThicknessOverrideLegacyKey = "materialThicknessOverride";
    public const string ToeKickHeightOverrideKey = "toeKickHeight";

    private static readonly Thickness DefaultPanelThickness = Thickness.Exact(Length.FromInches(0.75m));
    private static readonly Length DefaultToeKickHeight = Length.FromInches(4m);

    public static IReadOnlyList<PartGeometrySpec> BuildParts(CabinetStateRecord cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);

        var thickness = ResolveThickness(cabinet);
        var specs = cabinet.Category switch
        {
            CabinetCategory.Base => BuildBaseParts(cabinet, thickness),
            CabinetCategory.Wall => BuildWallParts(cabinet, thickness),
            CabinetCategory.Tall => BuildTallParts(cabinet, thickness),
            CabinetCategory.Vanity => BuildVanityParts(cabinet, thickness),
            _ => throw new NotSupportedException(
                $"Cabinet category '{cabinet.Category}' is not supported for part generation.")
        };

        if (cabinet.Construction == ConstructionMethod.FaceFrame)
        {
            specs.AddRange(BuildFaceFrameParts(cabinet, thickness));
        }

        return specs;
    }

    public static Thickness ResolveThickness(CabinetStateRecord cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);

        if (cabinet.EffectiveOverrides.TryGetValue(ThicknessOverrideKey, out var overrideValue) ||
            cabinet.EffectiveOverrides.TryGetValue(ThicknessOverrideLegacyKey, out overrideValue))
        {
            return overrideValue switch
            {
                OverrideValue.OfThickness thickness => thickness.Value,
                OverrideValue.OfLength length => Thickness.Exact(length.Value),
                _ => DefaultPanelThickness
            };
        }

        return DefaultPanelThickness;
    }

    private static List<PartGeometrySpec> BuildBaseParts(CabinetStateRecord cabinet, Thickness thickness)
    {
        var specs = BuildCaseParts(cabinet, thickness, ResolveShelfCount(cabinet), includeBottomShelfDepthRelief: true);
        specs.Add(new PartGeometrySpec(
            "ToeKick",
            InnerWidth(cabinet, thickness),
            ResolveToeKickHeight(cabinet),
            GrainDirection.None,
            NoEdges()));
        return specs;
    }

    private static List<PartGeometrySpec> BuildWallParts(CabinetStateRecord cabinet, Thickness thickness) =>
        BuildCaseParts(cabinet, thickness, ResolveShelfCount(cabinet), includeBottomShelfDepthRelief: true);

    private static List<PartGeometrySpec> BuildTallParts(CabinetStateRecord cabinet, Thickness thickness)
    {
        var specs = BuildCaseParts(cabinet, thickness, ResolveShelfCount(cabinet), includeBottomShelfDepthRelief: true);
        specs.Add(new PartGeometrySpec(
            "StructuralBase",
            InnerWidth(cabinet, thickness),
            ResolveToeKickHeight(cabinet),
            GrainDirection.None,
            NoEdges()));
        return specs;
    }

    private static List<PartGeometrySpec> BuildVanityParts(CabinetStateRecord cabinet, Thickness thickness)
    {
        var specs = BuildCaseParts(cabinet, thickness, shelfCount: 0, includeBottomShelfDepthRelief: false);
        specs.Add(new PartGeometrySpec(
            "ToeKick",
            InnerWidth(cabinet, thickness),
            ResolveToeKickHeight(cabinet),
            GrainDirection.None,
            NoEdges()));
        return specs;
    }

    private static List<PartGeometrySpec> BuildCaseParts(
        CabinetStateRecord cabinet,
        Thickness thickness,
        int shelfCount,
        bool includeBottomShelfDepthRelief)
    {
        var parts = new List<PartGeometrySpec>
        {
            new(
                "LeftSide",
                cabinet.NominalDepth,
                cabinet.EffectiveNominalHeight,
                GrainDirection.LengthWise,
                FrontVerticalEdge()),
            new(
                "RightSide",
                cabinet.NominalDepth,
                cabinet.EffectiveNominalHeight,
                GrainDirection.LengthWise,
                FrontVerticalEdge()),
            new(
                "Top",
                InnerWidth(cabinet, thickness),
                cabinet.NominalDepth,
                GrainDirection.LengthWise,
                FrontHorizontalEdge()),
            new(
                "Bottom",
                InnerWidth(cabinet, thickness),
                includeBottomShelfDepthRelief ? DepthLessBackClearance(cabinet, thickness) : cabinet.NominalDepth,
                GrainDirection.LengthWise,
                FrontHorizontalEdge()),
            new(
                "Back",
                InnerWidth(cabinet, thickness),
                InnerHeight(cabinet, thickness),
                GrainDirection.None,
                NoEdges())
        };

        for (var index = 0; index < shelfCount; index++)
        {
            parts.Add(new PartGeometrySpec(
                "AdjustableShelf",
                InnerWidth(cabinet, thickness),
                DepthLessBackClearance(cabinet, thickness),
                GrainDirection.LengthWise,
                FrontHorizontalEdge()));
        }

        return parts;
    }

    private static IReadOnlyList<PartGeometrySpec> BuildFaceFrameParts(CabinetStateRecord cabinet, Thickness thickness)
    {
        var openingCount = ResolveOpeningCount(cabinet);
        var railLength = cabinet.NominalWidth;
        var stileLength = cabinet.EffectiveNominalHeight;
        var faceFrameWidth = FaceFrameMemberWidth(cabinet);
        var parts = new List<PartGeometrySpec>
        {
            new("FrameStile", faceFrameWidth, stileLength, GrainDirection.LengthWise, NoEdges()),
            new("FrameStile", faceFrameWidth, stileLength, GrainDirection.LengthWise, NoEdges()),
            new("FrameRail", railLength, faceFrameWidth, GrainDirection.LengthWise, NoEdges()),
            new("FrameRail", railLength, faceFrameWidth, GrainDirection.LengthWise, NoEdges())
        };

        for (var index = 1; index < openingCount; index++)
        {
            parts.Add(new PartGeometrySpec(
                "FrameMullion",
                faceFrameWidth,
                ClampPositive(stileLength.Inches - (faceFrameWidth.Inches * 2m)),
                GrainDirection.LengthWise,
                NoEdges()));
        }

        return parts;
    }

    private static int ResolveShelfCount(CabinetStateRecord cabinet) =>
        cabinet.Category switch
        {
            CabinetCategory.Base => 1,
            CabinetCategory.Wall => cabinet.NominalWidth >= Length.FromInches(30m) ? 2 : 1,
            CabinetCategory.Tall => Math.Max(3, (int)Math.Floor((cabinet.EffectiveNominalHeight.Inches - 40m) / 16m) + 2),
            _ => 0
        };

    private static int ResolveOpeningCount(CabinetStateRecord cabinet) =>
        cabinet.Category switch
        {
            CabinetCategory.Base or CabinetCategory.Vanity => cabinet.NominalWidth >= Length.FromInches(30m) ? 2 : 1,
            CabinetCategory.Wall => cabinet.NominalWidth >= Length.FromInches(30m) ? 2 : 1,
            CabinetCategory.Tall => 2,
            _ => 1
        };

    private static Length ResolveToeKickHeight(CabinetStateRecord cabinet)
    {
        if (cabinet.EffectiveOverrides.TryGetValue(ToeKickHeightOverrideKey, out var overrideValue) &&
            overrideValue is OverrideValue.OfLength length &&
            length.Value > Length.Zero)
        {
            return length.Value;
        }

        return DefaultToeKickHeight;
    }

    private static Length FaceFrameMemberWidth(CabinetStateRecord cabinet) =>
        cabinet.NominalWidth >= Length.FromInches(30m)
            ? Length.FromInches(2m)
            : Length.FromInches(1.5m);

    private static Length InnerWidth(CabinetStateRecord cabinet, Thickness thickness) =>
        ClampPositive(cabinet.NominalWidth.Inches - (thickness.Actual.Inches * 2m));

    private static Length InnerHeight(CabinetStateRecord cabinet, Thickness thickness) =>
        ClampPositive(cabinet.EffectiveNominalHeight.Inches - (thickness.Actual.Inches * 2m));

    private static Length DepthLessBackClearance(CabinetStateRecord cabinet, Thickness thickness) =>
        ClampPositive(cabinet.NominalDepth.Inches - thickness.Actual.Inches);

    private static Length ClampPositive(decimal inches) =>
        inches > 0m ? Length.FromInches(inches) : Length.FromInches(0.125m);

    private static EdgeTreatment FrontVerticalEdge() =>
        new(null, null, null, FrontEdgeBandingId);

    private static EdgeTreatment FrontHorizontalEdge() =>
        new(FrontEdgeBandingId, null, null, null);

    private static EdgeTreatment NoEdges() =>
        new(null, null, null, null);
}

public sealed record PartGeometrySpec(
    string PartType,
    Length Width,
    Length Height,
    GrainDirection GrainDirection,
    EdgeTreatment Edges);
