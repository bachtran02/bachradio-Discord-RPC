using System.Text.Json.Serialization;

namespace BachRadio.Rpc.Models;

public record Track(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("artworkUrl")] string ArtworkUrl,
    [property: JsonPropertyName("length")] long Length,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("isStream")] bool IsStream
);

public record MusicStatus(
    [property: JsonPropertyName("position")] long Position,
    [property: JsonPropertyName("playing")] bool Playing,
    [property: JsonPropertyName("paused")] bool Paused,
    [property: JsonPropertyName("track")] Track Track
);
