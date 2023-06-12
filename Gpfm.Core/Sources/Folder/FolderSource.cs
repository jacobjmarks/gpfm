using System.Text.Json.Serialization;

namespace Gpfm.Core.Sources.Folder;

public record class FolderSource(
    string Name,
    bool Skip,
    [property: JsonPropertyName("folder")]
    string Folder
) : Source(Name, Skip)
{
    public const string TypeDiscriminator = "folder";
};
