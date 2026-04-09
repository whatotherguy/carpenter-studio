namespace CabinetDesigner.Persistence.Mapping;

internal static class LengthText
{
    public static string FormatLength(Length value) =>
        string.Create(CultureInfo.InvariantCulture, $"{decimal.Round(value.Inches, 4):0.0000}in");

    public static Length ParseLength(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!value.EndsWith("in", StringComparison.Ordinal))
        {
            throw new FormatException($"Unsupported length format '{value}'.");
        }

        return Length.FromInches(decimal.Parse(value[..^2], CultureInfo.InvariantCulture));
    }

    public static string FormatThickness(Thickness value) =>
        JsonSerializer.Serialize(
            new ThicknessPayload(FormatLength(value.Nominal), FormatLength(value.Actual)),
            SqliteJson.Options);

    public static Thickness ParseThickness(string value)
    {
        var payload = JsonSerializer.Deserialize<ThicknessPayload>(value, SqliteJson.Options)
            ?? throw new FormatException("Thickness payload was missing.");
        return new Thickness(ParseLength(payload.Nominal), ParseLength(payload.Actual));
    }

    public static string FormatPoint(Point2D point) =>
        string.Create(CultureInfo.InvariantCulture, $"{decimal.Round(point.X, 3):0.000},{decimal.Round(point.Y, 3):0.000}");

    public static Point2D ParsePoint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new FormatException($"Unsupported point format '{value}'.");
        }

        return new Point2D(
            decimal.Parse(parts[0], CultureInfo.InvariantCulture),
            decimal.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private sealed record ThicknessPayload(string Nominal, string Actual);
}
