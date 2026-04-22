using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class PropertyInspectorViewModelTests
{
    [Fact]
    public void SelectingSingleCabinet_PopulatesAllV1Properties()
    {
        var harness = CreateHarness();
        var cabinetId = harness.Service.SeedCabinet(
            typeId: "base-36",
            category: CabinetCategory.Base,
            construction: ConstructionMethod.Frameless,
            width: 36m,
            depth: 24m,
            height: 34.5m,
            openings:
            [
                new CabinetOpeningStateRecord(Guid.NewGuid(), 0, OpeningType.Door, Length.FromInches(18m), Length.FromInches(30m)),
                new CabinetOpeningStateRecord(Guid.NewGuid(), 1, OpeningType.Drawer, Length.FromInches(18m), Length.FromInches(12m))
            ],
            overrides: new Dictionary<string, OverrideValue>(StringComparer.Ordinal)
            {
                ["shelfCount"] = new OverrideValue.OfInt(2),
                ["toeKickHeight"] = new OverrideValue.OfLength(Length.FromInches(4.5m)),
                ["notes"] = new OverrideValue.OfString("site finish note"),
                ["material.Side"] = new OverrideValue.OfMaterialId(new MaterialId(Guid.NewGuid())),
                ["thickness.Side"] = new OverrideValue.OfLength(Length.FromInches(0.75m))
            });

        PublishProjectOpened(harness.EventBus);
        harness.ViewModel.OnSelectionChanged([cabinetId]);

        Assert.True(harness.ViewModel.HasSelection);
        Assert.True(harness.ViewModel.HasSingleSelection);
        Assert.Equal("base-36 #1", harness.ViewModel.DisplayName);
        Assert.Equal("36\"", harness.ViewModel.NominalWidth);
        Assert.Equal("24\"", harness.ViewModel.Depth);
        Assert.Equal("34.5\"", harness.ViewModel.Height);
        Assert.Equal(CabinetCategory.Base.ToString(), harness.ViewModel.Category);
        Assert.Equal(ConstructionMethod.Frameless.ToString(), harness.ViewModel.Construction);
        Assert.Equal(2, harness.ViewModel.Openings.Count);
        Assert.Equal("2", harness.ViewModel.ShelfCount);
        Assert.Equal("4.5\"", harness.ViewModel.ToeKickHeight);
        Assert.Equal(8, harness.ViewModel.MaterialOverrides.Count);
        Assert.Equal(8, harness.ViewModel.ThicknessOverrides.Count);
        Assert.Equal("site finish note", harness.ViewModel.Notes);
        Assert.Equal(12, harness.ViewModel.Properties.Count);
    }

    [Fact]
    public async Task EditingWidth_FiresResizeCabinetCommand_Once()
    {
        var harness = CreateHarness();
        var cabinetId = harness.Service.SeedCabinet("base-36", CabinetCategory.Base, ConstructionMethod.Frameless, 36m, 24m, 34.5m);
        PublishProjectOpened(harness.EventBus);
        harness.ViewModel.OnSelectionChanged([cabinetId]);

        harness.ViewModel.BeginNominalWidthEditCommand.Execute(null);
        harness.ViewModel.NominalWidthEditValue = "42";
        await harness.ViewModel.CommitNominalWidthEditCommand.ExecuteAsync();

        Assert.Equal(1, harness.Service.ResizeCalls);
        Assert.Equal(24m, harness.Service.LastResizeWidth);
        Assert.Equal(24m, harness.Service.GetCabinet(cabinetId)!.NominalWidth.Inches);
        Assert.Equal("24\"", harness.ViewModel.NominalWidth);
    }

    [Fact]
    public async Task EditingOpening_AddRemoveReorder_RoundTripsThroughCommandPipeline()
    {
        var harness = CreateHarness();
        var openingA = Guid.NewGuid();
        var openingB = Guid.NewGuid();
        var cabinetId = harness.Service.SeedCabinet(
            "base-36",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            36m,
            24m,
            34.5m,
            openings:
            [
                new CabinetOpeningStateRecord(openingA, 0, OpeningType.Door, Length.FromInches(18m), Length.FromInches(30m))
            ]);
        PublishProjectOpened(harness.EventBus);
        harness.ViewModel.OnSelectionChanged([cabinetId]);

        await harness.ViewModel.AddOpeningAsync(OpeningType.Drawer, 12m, 8m, null);
        var afterAdd = harness.Service.GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing after add.");
        Assert.Equal(2, afterAdd.EffectiveOpenings.Count);
        Assert.Equal(1, harness.Service.AddOpeningCalls);

        var newlyAdded = afterAdd.EffectiveOpenings.Single(opening => opening.Type == OpeningType.Drawer);
        await harness.ViewModel.ReorderOpeningAsync(newlyAdded.OpeningId, 0);
        var afterReorder = harness.Service.GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing after reorder.");
        Assert.Equal(newlyAdded.OpeningId, afterReorder.EffectiveOpenings[0].OpeningId);
        Assert.Equal(1, harness.Service.ReorderOpeningCalls);

        await harness.ViewModel.RemoveOpeningAsync(newlyAdded.OpeningId);
        var afterRemove = harness.Service.GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing after remove.");
        Assert.Single(afterRemove.EffectiveOpenings);
        Assert.Equal(openingA, afterRemove.EffectiveOpenings[0].OpeningId);
        Assert.Equal(1, harness.Service.RemoveOpeningCalls);
    }

    [Fact]
    public async Task SettingMaterialOverride_FiresSetOverrideCommand()
    {
        var harness = CreateHarness();
        var cabinetId = harness.Service.SeedCabinet("base-36", CabinetCategory.Base, ConstructionMethod.Frameless, 36m, 24m, 34.5m);
        PublishProjectOpened(harness.EventBus);
        harness.ViewModel.OnSelectionChanged([cabinetId]);
        var materialId = Guid.NewGuid();

        await harness.ViewModel.SetMaterialOverrideAsync("Side", materialId);

        Assert.Equal(1, harness.Service.SetOverrideCalls);
        Assert.Equal("material.Side", harness.Service.LastSetOverrideKey);
        Assert.IsType<OverrideValueDto.OfMaterialId>(harness.Service.LastSetOverrideValue);
        Assert.Equal(materialId, ((OverrideValueDto.OfMaterialId)harness.Service.LastSetOverrideValue!).MaterialId);
        Assert.Equal(new MaterialId(materialId), harness.Service.GetCabinet(cabinetId)!.EffectiveOverrides["material.Side"].As<OverrideValue.OfMaterialId>().Value);
    }

    [Fact]
    public async Task ClearingMaterialOverride_FiresRemoveOverrideCommand()
    {
        var harness = CreateHarness();
        var cabinetId = harness.Service.SeedCabinet(
            "base-36",
            CabinetCategory.Base,
            ConstructionMethod.Frameless,
            36m,
            24m,
            34.5m,
            overrides: new Dictionary<string, OverrideValue>(StringComparer.Ordinal)
            {
                ["material.Side"] = new OverrideValue.OfMaterialId(new MaterialId(Guid.NewGuid()))
            });
        PublishProjectOpened(harness.EventBus);
        harness.ViewModel.OnSelectionChanged([cabinetId]);

        await harness.ViewModel.SetMaterialOverrideAsync("Side", null);

        Assert.Equal(1, harness.Service.RemoveOverrideCalls);
        Assert.DoesNotContain("material.Side", harness.Service.GetCabinet(cabinetId)!.EffectiveOverrides.Keys);
        Assert.True(harness.ViewModel.MaterialOverrides.Single(row => row.Key == "material.Side").IsPlaceholder);
    }

    [Fact]
    public async Task MultiSelect_ShowsBlankForDifferingValues_AndEditAppliesToAll()
    {
        var harness = CreateHarness();
        var first = harness.Service.SeedCabinet("base-36", CabinetCategory.Base, ConstructionMethod.Frameless, 36m, 24m, 34.5m);
        var second = harness.Service.SeedCabinet("base-36", CabinetCategory.Base, ConstructionMethod.Frameless, 30m, 24m, 34.5m);
        PublishProjectOpened(harness.EventBus);
        harness.ViewModel.OnSelectionChanged([first, second]);

        Assert.NotEqual("36\"", harness.ViewModel.NominalWidth);
        Assert.NotEqual("30\"", harness.ViewModel.NominalWidth);

        await harness.ViewModel.SetNominalWidthAsync(42m);

        Assert.Equal(2, harness.Service.ResizeCalls);
        Assert.Equal(24m, harness.Service.GetCabinet(first)!.NominalWidth.Inches);
        Assert.Equal(24m, harness.Service.GetCabinet(second)!.NominalWidth.Inches);
        Assert.Equal(24m, harness.Service.GetCabinet(first)!.NominalDepth.Inches);
    }

    [Fact]
    public async Task UndoAfterWidthChange_RestoresPreviousWidth()
    {
        var harness = CreateHarness();
        var cabinetId = harness.Service.SeedCabinet("base-36", CabinetCategory.Base, ConstructionMethod.Frameless, 36m, 24m, 34.5m);
        PublishProjectOpened(harness.EventBus);
        harness.ViewModel.OnSelectionChanged([cabinetId]);

        harness.ViewModel.BeginNominalWidthEditCommand.Execute(null);
        harness.ViewModel.NominalWidthEditValue = "42";
        await harness.ViewModel.CommitNominalWidthEditCommand.ExecuteAsync();
        Assert.Equal("24\"", harness.ViewModel.NominalWidth);

        harness.Service.UndoLastResize();
        harness.EventBus.Publish(new UndoAppliedEvent(CommandResultDto.NoOp("undo")));
        harness.ViewModel.OnSelectionChanged([cabinetId]);

        Assert.Equal(36m, harness.Service.GetCabinet(cabinetId)!.NominalWidth.Inches);
        Assert.Equal(36m, harness.ViewModel.NominalWidthEditValue.ToDecimal());
    }

    private static void PublishProjectOpened(ApplicationEventBus eventBus) =>
        eventBus.Publish(new ProjectOpenedEvent(new ProjectSummaryDto(
            Guid.NewGuid(),
            "Demo Project",
            "C:\\demo.cab",
            DateTimeOffset.UtcNow,
            "Rev 1",
            false)));

    private static TestHarness CreateHarness()
    {
        var eventBus = new ApplicationEventBus();
        var logger = new CapturingAppLogger();
        var service = new RecordingCabinetPropertyService();
        var viewModel = new PropertyInspectorViewModel(service, eventBus, logger);
        return new TestHarness(eventBus, service, viewModel);
    }

    private sealed record TestHarness(
        ApplicationEventBus EventBus,
        RecordingCabinetPropertyService Service,
        PropertyInspectorViewModel ViewModel);

    private sealed class RecordingCabinetPropertyService : ICabinetPropertyService
    {
        private readonly List<CabinetStateRecord> _cabinets = [];
        private readonly Stack<CabinetStateRecord> _resizeUndo = [];

        public int ResizeCalls { get; private set; }

        public decimal LastResizeWidth { get; private set; }

        public int SetOverrideCalls { get; private set; }

        public int RemoveOverrideCalls { get; private set; }

        public int AddOpeningCalls { get; private set; }

        public int RemoveOpeningCalls { get; private set; }

        public int ReorderOpeningCalls { get; private set; }

        public string? LastSetOverrideKey { get; private set; }

        public OverrideValueDto? LastSetOverrideValue { get; private set; }

        public IReadOnlyList<CabinetStateRecord> GetAllCabinets() => _cabinets.OrderBy(cabinet => cabinet.CabinetTypeId).ThenBy(cabinet => cabinet.CabinetId.Value).ToArray();

        public CabinetStateRecord? GetCabinet(Guid cabinetId) => _cabinets.FirstOrDefault(cabinet => cabinet.CabinetId.Value == cabinetId);

        public IReadOnlyList<CabinetStateRecord> GetCabinets(IReadOnlyList<Guid> cabinetIds) =>
            cabinetIds.Select(id => GetCabinet(id)).Where(cabinet => cabinet is not null).Select(cabinet => cabinet!).ToArray();

        public Task<CommandResultDto> ResizeCabinetAsync(Guid cabinetId, decimal widthInches, decimal depthInches, decimal heightInches)
        {
            ResizeCalls++;
            LastResizeWidth = widthInches;
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            _resizeUndo.Push(cabinet);
            ReplaceCabinet(cabinet with
            {
                NominalWidth = Length.FromInches(widthInches),
                NominalDepth = Length.FromInches(depthInches),
                NominalHeight = Length.FromInches(heightInches)
            });
            return Task.FromResult(Success("resize_cabinet"));
        }

        public Task<CommandResultDto> SetCabinetCategoryAsync(Guid cabinetId, CabinetCategory category)
        {
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            ReplaceCabinet(cabinet with { Category = category });
            return Task.FromResult(Success("set_cabinet_category"));
        }

        public Task<CommandResultDto> SetCabinetConstructionAsync(Guid cabinetId, ConstructionMethod construction)
        {
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            ReplaceCabinet(cabinet with { Construction = construction });
            return Task.FromResult(Success("set_cabinet_construction"));
        }

        public Task<CommandResultDto> AddOpeningAsync(Guid cabinetId, OpeningType openingType, decimal widthInches, decimal heightInches, int? insertIndex)
        {
            AddOpeningCalls++;
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            var openings = cabinet.EffectiveOpenings.OrderBy(opening => opening.Index).ToList();
            var index = insertIndex is null ? openings.Count : Math.Clamp(insertIndex.Value, 0, openings.Count);
            var opening = new CabinetOpeningStateRecord(Guid.NewGuid(), index, openingType, Length.FromInches(widthInches), Length.FromInches(heightInches));
            openings.Insert(index, opening);
            ReplaceCabinet(cabinet with { Openings = openings.Select((entry, i) => entry with { Index = i }).ToArray() });
            return Task.FromResult(Success("add_opening"));
        }

        public Task<CommandResultDto> RemoveOpeningAsync(Guid cabinetId, Guid openingId)
        {
            RemoveOpeningCalls++;
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            var openings = cabinet.EffectiveOpenings.Where(opening => opening.OpeningId != openingId).Select((entry, i) => entry with { Index = i }).ToArray();
            ReplaceCabinet(cabinet with { Openings = openings });
            return Task.FromResult(Success("remove_opening"));
        }

        public Task<CommandResultDto> ReorderOpeningAsync(Guid cabinetId, Guid openingId, int newIndex)
        {
            ReorderOpeningCalls++;
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            var openings = cabinet.EffectiveOpenings.OrderBy(opening => opening.Index).ToList();
            var opening = openings.Single(candidate => candidate.OpeningId == openingId);
            openings.Remove(opening);
            openings.Insert(Math.Clamp(newIndex, 0, openings.Count), opening);
            ReplaceCabinet(cabinet with { Openings = openings.Select((entry, i) => entry with { Index = i }).ToArray() });
            return Task.FromResult(Success("reorder_opening"));
        }

        public Task<CommandResultDto> SetCabinetOverrideAsync(Guid cabinetId, string overrideKey, OverrideValueDto value)
        {
            SetOverrideCalls++;
            LastSetOverrideKey = overrideKey;
            LastSetOverrideValue = value;
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            var overrides = new Dictionary<string, OverrideValue>(cabinet.EffectiveOverrides, StringComparer.Ordinal)
            {
                [overrideKey] = MapOverrideValue(value)
            };
            ReplaceCabinet(cabinet with { Overrides = overrides });
            return Task.FromResult(Success("set_cabinet_override"));
        }

        public Task<CommandResultDto> RemoveCabinetOverrideAsync(Guid cabinetId, string overrideKey)
        {
            RemoveOverrideCalls++;
            var cabinet = GetCabinet(cabinetId) ?? throw new InvalidOperationException("Cabinet missing.");
            var overrides = new Dictionary<string, OverrideValue>(cabinet.EffectiveOverrides, StringComparer.Ordinal);
            overrides.Remove(overrideKey);
            ReplaceCabinet(cabinet with { Overrides = overrides });
            return Task.FromResult(Success("remove_cabinet_override"));
        }

        public void UndoLastResize()
        {
            if (_resizeUndo.Count == 0)
            {
                return;
            }

            ReplaceCabinet(_resizeUndo.Pop());
        }

        public Guid SeedCabinet(
            string typeId,
            CabinetCategory category,
            ConstructionMethod construction,
            decimal width,
            decimal depth,
            decimal height,
            IReadOnlyList<CabinetOpeningStateRecord>? openings = null,
            IReadOnlyDictionary<string, OverrideValue>? overrides = null)
        {
            var cabinetId = Guid.NewGuid();
            _cabinets.Add(new CabinetStateRecord(
                new CabinetId(cabinetId),
                typeId,
                Length.FromInches(width),
                Length.FromInches(depth),
                new RunId(Guid.NewGuid()),
                new RunSlotId(Guid.NewGuid()),
                category,
                construction,
                Length.FromInches(height),
                openings,
                overrides,
                1));
            return cabinetId;
        }

        private void ReplaceCabinet(CabinetStateRecord cabinet)
        {
            var index = _cabinets.FindIndex(candidate => candidate.CabinetId == cabinet.CabinetId);
            if (index >= 0)
            {
                _cabinets[index] = cabinet;
            }
        }

        private static CommandResultDto Success(string commandType) =>
            new(Guid.NewGuid(), commandType, true, [], [], []);

        private static OverrideValue MapOverrideValue(OverrideValueDto value) =>
            value switch
            {
                OverrideValueDto.OfDecimalInches inches => new OverrideValue.OfLength(Length.FromInches(inches.Inches)),
                OverrideValueDto.OfString text => new OverrideValue.OfString(text.Value),
                OverrideValueDto.OfBool boolean => new OverrideValue.OfBool(boolean.Value),
                OverrideValueDto.OfInt integer => new OverrideValue.OfInt(integer.Value),
                OverrideValueDto.OfMaterialId material => new OverrideValue.OfMaterialId(new MaterialId(material.MaterialId)),
                OverrideValueDto.OfHardwareItemId hardware => new OverrideValue.OfHardwareItemId(new HardwareItemId(hardware.HardwareItemId)),
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
    }
}

internal static class OverrideValueExtensions
{
    public static T As<T>(this OverrideValue value) where T : OverrideValue => Assert.IsType<T>(value);
}

internal static class DecimalExtensions
{
    public static decimal ToDecimal(this string value) => decimal.Parse(value.TrimEnd('"'));
}
