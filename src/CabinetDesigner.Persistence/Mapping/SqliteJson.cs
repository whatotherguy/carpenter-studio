namespace CabinetDesigner.Persistence.Mapping;

internal static class SqliteJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new OverrideValueJsonConverter());
        options.Converters.Add(new DeltaValueJsonConverter());
        return options;
    }
}
