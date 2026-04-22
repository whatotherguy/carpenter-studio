using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.IO;

namespace CabinetDesigner.Application.Packaging;

public static class DeterministicJson
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        TypeInfoResolver = CreateResolver()
    };

    public static byte[] Serialize<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, SerializeOptions)
            ?? throw new InvalidOperationException("DeterministicJson could not serialize a null JSON node.");

        if (node is JsonObject jsonObject &&
            !jsonObject.ContainsKey("schema_version"))
        {
            jsonObject["schema_version"] = 1;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteNode(writer, node);
        }

        return stream.ToArray();
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

    private static IJsonTypeInfoResolver CreateResolver()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            var orderedProperties = typeInfo.Properties
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();

            typeInfo.Properties.Clear();
            foreach (var property in orderedProperties)
            {
                typeInfo.Properties.Add(property);
            }
        });

        return resolver;
    }
}
