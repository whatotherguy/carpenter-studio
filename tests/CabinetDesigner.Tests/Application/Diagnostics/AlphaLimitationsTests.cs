using CabinetDesigner.Application.Diagnostics;
using Xunit;

namespace CabinetDesigner.Tests.Application.Diagnostics;

public sealed class AlphaLimitationsTests
{
    [Fact]
    public void Codes_AreUnique()
    {
        var codes = AlphaLimitations.All.Select(limitation => limitation.Code).ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryAlphaArea_IsRepresented()
    {
        var representedAreas = AlphaLimitations.All.Select(limitation => limitation.Area).Distinct().ToArray();

        Assert.Equal(Enum.GetValues<AlphaArea>().OrderBy(area => area).ToArray(), representedAreas);
    }

    [Fact]
    public void ApplicationServices_NoLongerContain_NotImplementedException_Throws()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var serviceFiles = Directory.EnumerateFiles(
            Path.Combine(repoRoot, "src", "CabinetDesigner.Application", "Services"),
            "*.cs",
            SearchOption.AllDirectories);

        var offenders = new List<string>();
        foreach (var file in serviceFiles)
        {
            var content = File.ReadAllText(file);
            if (content.Contains("throw new NotImplementedException", StringComparison.Ordinal) ||
                content.Contains("catch (NotImplementedException", StringComparison.Ordinal))
            {
                offenders.Add(file);
            }
        }

        Assert.Empty(offenders);
    }
}
