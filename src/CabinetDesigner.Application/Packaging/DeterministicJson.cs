using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CabinetDesigner.Application.Packaging;

public static class DeterministicJson
{
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, SerializeOptions)
            ?? throw new InvalidOperationException("DeterministicJson could not serialize a null JSON node.");

        return SerializeNode(node);
    }

    public static byte[] SerializeToUtf8Bytes<T>(T value) =>
        Encoding.UTF8.GetBytes(Serialize(value));

    private static string SerializeNode(JsonNode node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            WriteNode(writer, node);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteNode(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                return;
            case JsonObject jsonObject:
                writer.WriteStartObject();
                foreach (var property in jsonObject.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    WriteNode(writer, property.Value);
                }

                writer.WriteEndObject();
                return;
            case JsonArray jsonArray:
                writer.WriteStartArray();
                foreach (var item in jsonArray)
                {
                    WriteNode(writer, item);
                }

                writer.WriteEndArray();
                return;
            case JsonValue jsonValue:
                jsonValue.WriteTo(writer);
                return;
            default:
                throw new InvalidOperationException($"Unsupported JsonNode type '{node.GetType().Name}'.");
        }
    }
}
