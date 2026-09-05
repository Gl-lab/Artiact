using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Artiact.SmartProxy.Models;

public sealed class RoundTripDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
    {
        return DateTime.Parse( reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind );
    }

    public override void Write( Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options )
    {
        writer.WriteStringValue( value.ToUniversalTime().ToString( "O", CultureInfo.InvariantCulture ) );
    }
}
