using System.CommandLine;
using System.Text.Json;
using Gpfm.Core;

var configFileOption = new Option<FileInfo>(new[] { "--configFile", "-c" })
{
    Description = "Configuration file for the job.",
    IsRequired = true,
};

var outDirectoryOption = new Option<DirectoryInfo>(new[] { "--outDir", "-o" })
{
    Description = "Directory in which to store the merged output."
        + "\nWARNING: Directory will be deleted when the job starts.",
    IsRequired = true,
};

var rootCommand = new RootCommand("General Purpose File/Folder Merger (GPFM) CLI")
{
    Name = "gpfm",
};
rootCommand.AddOption(configFileOption);
rootCommand.AddOption(outDirectoryOption);

rootCommand.SetHandler(async (configFile, outDirectory) =>
{
    var configJson = await File.ReadAllTextAsync(configFile.FullName);
    var config = JsonSerializer.Deserialize<JobConfig>(configJson)
        ?? throw new JsonException("Deserialized object instance is null");

    config = config with { Output = outDirectory.FullName };

    await Job.RunAsync(config);
}, configFileOption, outDirectoryOption);

return await rootCommand.InvokeAsync(args);
