namespace CabinetDesigner.Persistence.Mapping;

internal sealed class DeltaValueJsonConverter : JsonConverter<DeltaValue>
{
    public override DeltaValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var type = root.GetProperty("type").GetString();

        return type switch
        {
            "length" => new DeltaValue.OfLength(LengthText.ParseLength(root.GetProperty("value").GetString()!)),
            "thickness" => new DeltaValue.OfThickness(LengthText.ParseThickness(root.GetProperty("value").GetString()!)),
            "string" => new DeltaValue.OfString(root.GetProperty("value").GetString()!),
            "bool" => new DeltaValue.OfBool(root.GetProperty("value").GetBoolean()),
            "int" => new DeltaValue.OfInt(root.GetProperty("value").GetInt32()),
            "decimal" => new DeltaValue.OfDecimal(root.GetProperty("value").GetDecimal()),
            "null" => new DeltaValue.Null(),
            _ => throw new JsonException($"Unknown delta value type '{type}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, DeltaValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case DeltaValue.OfLength length:
                writer.WriteString("type", "length");
                writer.WriteString("value", LengthText.FormatLength(length.Value));
                break;
            case DeltaValue.OfThickness thickness:
                writer.WriteString("type", "thickness");
                writer.WriteString("value", LengthText.FormatThickness(thickness.Value));
                break;
            case DeltaValue.OfString text:
                writer.WriteString("type", "string");
                writer.WriteString("value", text.Value);
                break;
            case DeltaValue.OfBool flag:
                writer.WriteString("type", "bool");
                writer.WriteBoolean("value", flag.Value);
                break;
            case DeltaValue.OfInt number:
                writer.WriteString("type", "int");
                writer.WriteNumber("value", number.Value);
                break;
            case DeltaValue.OfDecimal decimalValue:
                writer.WriteString("type", "decimal");
                writer.WriteNumber("value", decimal.Round(decimalValue.Value, 6));
                break;
            case DeltaValue.Null:
                writer.WriteString("type", "null");
                writer.WriteNull("value");
                break;
            default:
                throw new JsonException($"Unsupported delta value type '{value.GetType().Name}'.");
        }

        writer.WriteEndObject();
    }
}
