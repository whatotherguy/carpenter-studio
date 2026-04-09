namespace CabinetDesigner.Persistence.Mapping;

internal sealed class OverrideValueJsonConverter : JsonConverter<OverrideValue>
{
    public override OverrideValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var type = root.GetProperty("type").GetString();

        return type switch
        {
            "length" => new OverrideValue.OfLength(LengthText.ParseLength(root.GetProperty("value").GetString()!)),
            "thickness" => new OverrideValue.OfThickness(LengthText.ParseThickness(root.GetProperty("value").GetString()!)),
            "angle" => new OverrideValue.OfAngle(Angle.FromDegrees(root.GetProperty("value").GetDecimal())),
            "string" => new OverrideValue.OfString(root.GetProperty("value").GetString()!),
            "bool" => new OverrideValue.OfBool(root.GetProperty("value").GetBoolean()),
            "int" => new OverrideValue.OfInt(root.GetProperty("value").GetInt32()),
            "decimal" => new OverrideValue.OfDecimal(root.GetProperty("value").GetDecimal()),
            "material_id" => new OverrideValue.OfMaterialId(new MaterialId(Guid.Parse(root.GetProperty("value").GetString()!))),
            "hardware_item_id" => new OverrideValue.OfHardwareItemId(new HardwareItemId(Guid.Parse(root.GetProperty("value").GetString()!))),
            _ => throw new JsonException($"Unknown override value type '{type}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, OverrideValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case OverrideValue.OfLength length:
                writer.WriteString("type", "length");
                writer.WriteString("value", LengthText.FormatLength(length.Value));
                break;
            case OverrideValue.OfThickness thickness:
                writer.WriteString("type", "thickness");
                writer.WriteString("value", LengthText.FormatThickness(thickness.Value));
                break;
            case OverrideValue.OfAngle angle:
                writer.WriteString("type", "angle");
                writer.WriteNumber("value", decimal.Round(angle.Value.Degrees, 6));
                break;
            case OverrideValue.OfString text:
                writer.WriteString("type", "string");
                writer.WriteString("value", text.Value);
                break;
            case OverrideValue.OfBool flag:
                writer.WriteString("type", "bool");
                writer.WriteBoolean("value", flag.Value);
                break;
            case OverrideValue.OfInt number:
                writer.WriteString("type", "int");
                writer.WriteNumber("value", number.Value);
                break;
            case OverrideValue.OfDecimal decimalValue:
                writer.WriteString("type", "decimal");
                writer.WriteNumber("value", decimal.Round(decimalValue.Value, 6));
                break;
            case OverrideValue.OfMaterialId materialId:
                writer.WriteString("type", "material_id");
                writer.WriteString("value", materialId.Value.Value);
                break;
            case OverrideValue.OfHardwareItemId hardwareId:
                writer.WriteString("type", "hardware_item_id");
                writer.WriteString("value", hardwareId.Value.Value);
                break;
            default:
                throw new JsonException($"Unsupported override value type '{value.GetType().Name}'.");
        }

        writer.WriteEndObject();
    }
}
