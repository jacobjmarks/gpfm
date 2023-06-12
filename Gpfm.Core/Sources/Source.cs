using System.Text.Json.Serialization;
using Gpfm.Core.Sources.File;
using Gpfm.Core.Sources.Folder;
using Gpfm.Core.Sources.GitHub;
using Gpfm.Core.Sources.Url;

namespace Gpfm.Core.Sources;

[JsonPolymorphic(TypeDiscriminatorPropertyName = TypeDiscriminatorPropertyName)]
[JsonDerivedType(typeof(GitHubSource), GitHubSource.TypeDiscriminator)]
[JsonDerivedType(typeof(UrlSource), UrlSource.TypeDiscriminator)]
[JsonDerivedType(typeof(FileSource), FileSource.TypeDiscriminator)]
[JsonDerivedType(typeof(FolderSource), FolderSource.TypeDiscriminator)]
public record class Source(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("skip")]
    bool Skip
)
{
    public const string TypeDiscriminatorPropertyName = "type";
};
