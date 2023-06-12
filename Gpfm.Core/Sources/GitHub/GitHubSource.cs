using System.Text.Json.Serialization;

namespace Gpfm.Core.Sources.GitHub;

public record class GitHubSource(
    string Name,
    bool Skip,
    [property: JsonPropertyName("repository")]
    Uri Repository,
    [property: JsonPropertyName("includePreRelease")]
    bool IncludePreRelease,
    [property: JsonPropertyName("tag")]
    string? Tag,
    [property: JsonPropertyName("asset")]
    string Asset
) : Source(Name, Skip)
{
    public const string TypeDiscriminator = "gitHub";
};
