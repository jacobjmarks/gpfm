using System.Text.Json.Serialization;

namespace Gpfm.Core.Sources.Url;

public record class UrlSource(
    string Name,
    bool Skip,
    [property: JsonPropertyName("url")]
    Uri Url
) : Source(Name, Skip)
{
    public const string TypeDiscriminator = "url";
};
