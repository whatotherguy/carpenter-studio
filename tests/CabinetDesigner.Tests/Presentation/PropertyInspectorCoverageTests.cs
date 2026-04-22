using System.Reflection;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class PropertyInspectorCoverageTests
{
    [Fact]
    public void AllV1InspectorFields_ArePresent_AndBindable()
    {
        var expected = new[]
        {
            "DisplayName",
            "NominalWidth",
            "Depth",
            "Height",
            "Category",
            "Construction",
            "Openings",
            "ShelfCount",
            "ToeKickHeight",
            "MaterialOverrides",
            "ThicknessOverrides",
            "Notes"
        };

        var properties = typeof(PropertyInspectorViewModel).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (var name in expected)
        {
            var property = Assert.Single(properties.Where(candidate => candidate.Name == name));
            Assert.NotNull(property.GetMethod);
            Assert.True(property.GetMethod!.IsPublic);
        }
    }
}
