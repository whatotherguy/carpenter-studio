using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class AlphaLimitationsDialogViewModelTests
{
    [Fact]
    public void Groups_ByArea_AndOrdersDeterministically()
    {
        AlphaLimitation[] limitations =
        [
            new AlphaLimitation("CODE-3", "Third", "Third message.", AlphaArea.Export),
            new AlphaLimitation("CODE-1", "First", "First message.", AlphaArea.Properties),
            new AlphaLimitation("CODE-2", "Second", "Second message.", AlphaArea.Export),
            new AlphaLimitation("CODE-0", "Zero", "Zero message.", AlphaArea.Editor)
        ];

        var viewModel = new AlphaLimitationsDialogViewModel(null, limitations);

        Assert.Equal(new[] { AlphaArea.Editor, AlphaArea.Properties, AlphaArea.Export }, viewModel.Groups.Select(group => group.Area).ToArray());
        Assert.Equal(new[] { "CODE-0" }, viewModel.Groups[0].Limitations.Select(limitation => limitation.Code).ToArray());
        Assert.Equal(new[] { "CODE-1" }, viewModel.Groups[1].Limitations.Select(limitation => limitation.Code).ToArray());
        Assert.Equal(new[] { "CODE-2", "CODE-3" }, viewModel.Groups[2].Limitations.Select(limitation => limitation.Code).ToArray());
    }
}
