using System.CommandLine;
using System.Text.Json;
using Gpfm.Core;

var configFileOption = new CliOption<FileInfo>("--configFile", "-c")
{
    Description = "Configuration file for the job.",
    Required = true,
};

var outDirectoryOption = new CliOption<DirectoryInfo>("--outDir", "-o")
{
    Description = "Directory in which to store the merged output.",
    Required = false,
};

var rootCommand = new CliRootCommand("General Purpose File/Folder Merger (GPFM) CLI")
{
    configFileOption,
    outDirectoryOption,
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var configFile = parseResult.GetValue(configFileOption)
        ?? throw new ArgumentException(configFileOption.Name);
    var outDirectory = parseResult.GetValue(outDirectoryOption);

    var configJson = await File.ReadAllTextAsync(configFile.FullName, cancellationToken);
    var config = JsonSerializer.Deserialize<JobConfig>(configJson)
        ?? throw new JsonException("Deserialized object instance is null");

    if (outDirectory != null)
        config = config with { Output = outDirectory.FullName };

    await Job.RunAsync(config, cancellationToken);
});

var configuration = new CliConfiguration(rootCommand);
return await configuration.InvokeAsync(args);
