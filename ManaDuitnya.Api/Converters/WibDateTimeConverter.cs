using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManaDuitnya.Api.Converters;

public class WibDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format =  "yyyy-MM-dd HH:mm:ss";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}