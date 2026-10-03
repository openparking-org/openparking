using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenParking.Api.Configuration;

/// <summary>
/// JSON settings for every API response, in one place so tests exercise the
/// same configuration the running API uses.
/// </summary>
public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());

        // Several controllers return EF entities whose navigation properties
        // point back at each other (Booking -> Slot -> Zone -> Slots -> ...).
        // Without this the serializer throws "A possible object cycle was
        // detected" after the work is already committed, so a booking that was
        // saved and reserved its slot still reached the client as a 500, and a
        // retry then failed with "slot not available". IgnoreCycles writes null
        // for the back-reference and keeps the existing response shape.
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    }
}
