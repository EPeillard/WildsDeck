using System.Text.Json.Serialization;

namespace WildsDeck.Core;

[JsonConverter(typeof(JsonStringEnumConverter<SupportStatus>))]
public enum SupportStatus
{
    Unsupported,
    Experimental,
    Supported
}

