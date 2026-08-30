using System.Text.Json.Serialization;

namespace WildsDeck.Core;

[JsonConverter(typeof(JsonStringEnumConverter<GameMode>))]
public enum GameMode
{
    Unknown,
    Town,
    Hunt
}

