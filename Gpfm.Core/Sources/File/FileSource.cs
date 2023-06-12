using System.Text.Json.Serialization;

namespace Gpfm.Core.Sources.File;

public record class FileSource(
    string Name,
    bool Skip,
    [property: JsonPropertyName("file")]
    string File
) : Source(Name, Skip)
{
    public const string TypeDiscriminator = "file";
};
