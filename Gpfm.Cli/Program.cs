using System.CommandLine;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Gpfm.Cli;

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

public record class UrlSource(
    string Name,
    bool Skip,
    [property: JsonPropertyName("url")]
    Uri Url
) : Source(Name, Skip)
{
    public const string TypeDiscriminator = "url";
};

public record class FileSource(
    string Name,
    bool Skip,
    [property: JsonPropertyName("file")]
    string File
) : Source(Name, Skip)
{
    public const string TypeDiscriminator = "file";
};

public record class FolderSource(
    string Name,
    bool Skip,
    [property: JsonPropertyName("folder")]
    string Folder
) : Source(Name, Skip)
{
    public const string TypeDiscriminator = "folder";
};

public record class JobConfig(
    [property: JsonPropertyName("input")]
    ICollection<Source> Sources,
    [property: JsonPropertyName("output")]
    string Output
);

public class Job
{
    private readonly string _tempDirectory = Path.Join(Path.GetTempPath(), "gpfm", Guid.NewGuid().ToString());

    private readonly JobConfig _config;

    public Job(JobConfig config)
    {
        _config = config;
    }

    public static async Task RunAsync(JobConfig config)
    {
        var job = new Job(config);
        await job.RunAsync();
    }

    public async Task RunAsync()
    {
        if (Directory.Exists(_config.Output))
            Directory.Delete(_config.Output, recursive: true);
        Directory.CreateDirectory(_config.Output);

        var i = 0;
        foreach (var source in _config.Sources)
        {
            Console.WriteLine($"[{i + 1:D2}] Processing source: {source}");

            if (source.Skip)
            {
                Console.WriteLine("Skipping ...");
                i++;
                continue;
            }

            switch (source)
            {
                case GitHubSource gitHubSource:
                    await ProcessSourceAsync(gitHubSource);
                    break;
                case UrlSource urlSource:
                    await ProcessSourceAsync(urlSource);
                    break;
                case FileSource fileSource:
                    await ProcessSourceAsync(fileSource);
                    break;
                case FolderSource folderSource:
                    await ProcessSourceAsync(folderSource);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected source type '{source.GetType()}'");
            }
            i++;
        }
    }

    private async Task ProcessSourceAsync(GitHubSource source)
    {
        var path = source.Repository.LocalPath.Trim('/').Split('/');
        if (path.Length != 2 || !path.All(p => !string.IsNullOrWhiteSpace(p)))
            throw new ArgumentException($"Invalid GitHub repository '{source.Repository}'");

        var (owner, repo) = (path[0], path[1]);

        GitHubRelease? release;

        if (!string.IsNullOrEmpty(source.Tag))
            release = await GitHubApiClient.GetReleaseByTagAsync(owner, repo, source.Tag);
        else if (source.IncludePreRelease)
            release = (await GitHubApiClient.GetReleasesAsync(owner, repo)).First();
        else
            release = await GitHubApiClient.GetLatestReleaseAsync(owner, repo);

        Console.WriteLine($"Using release: {release.Name} ({release.TagName})");

        var asset = release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name, source.Asset))
            ?? throw new InvalidOperationException($"No asset found matching pattern '{source.Asset}'");

        var stagingDirectory = new DirectoryInfo(Path.Join(_tempDirectory, source.Name));
        if (!stagingDirectory.Exists) stagingDirectory.Create();

        var stagingFile = new FileInfo(Path.Join(stagingDirectory.FullName, asset.Name));

        Console.WriteLine($"Downloading asset '{asset.Name}' ...");

        using (var httpClient = new HttpClient())
        using (var fileDownloadStream = await httpClient.GetStreamAsync(asset.DownloadUrl))
        using (var stagingFileStream = stagingFile.OpenWrite())
            await fileDownloadStream.CopyToAsync(stagingFileStream);

        Console.WriteLine($"Extracting ...");

        var extractDirectory = _config.Output;
        ZipFile.ExtractToDirectory(stagingFile.FullName, extractDirectory, overwriteFiles: true);
    }

    private async Task ProcessSourceAsync(UrlSource source)
    {
        var stagingDirectory = new DirectoryInfo(Path.Join(_tempDirectory, source.Name));
        if (!stagingDirectory.Exists) stagingDirectory.Create();

        var stagingFile = new FileInfo(Path.Join(stagingDirectory.FullName, source.Url.Segments.Last()));
        if (string.IsNullOrEmpty(stagingFile.Extension))
            stagingFile = new(stagingFile.FullName + ".tmp");

        Console.WriteLine($"Downloading {source.Url} ...");
        // TODO better infer filename from Content-Disposition

        using (var httpClient = new HttpClient())
        using (var fileDownloadStream = await httpClient.GetStreamAsync(source.Url))
        using (var stagingFileStream = stagingFile.OpenWrite())
            await fileDownloadStream.CopyToAsync(stagingFileStream);

        Console.WriteLine($"Extracting ...");

        var extractDirectory = _config.Output;
        ZipFile.ExtractToDirectory(stagingFile.FullName, extractDirectory, overwriteFiles: true);
    }

    private Task ProcessSourceAsync(FileSource source)
    {
        var file = new FileInfo(source.File);
        if (!file.Exists)
            throw new ArgumentException($"File does not exist: '{source.File}'", nameof(source));

        Console.WriteLine("Copying file ...");

        var outFile = Path.Join(_config.Output, file.Name);
        file.CopyTo(outFile, overwrite: true);
        return Task.CompletedTask;
    }

    private Task ProcessSourceAsync(FolderSource source)
    {
        var folder = new DirectoryInfo(source.Folder);
        if (!folder.Exists)
            throw new ArgumentException($"Folder does not exist: '{source.Folder}'", nameof(source));

        Console.WriteLine("Copying folder ...");

        CopyDirectory(folder, new(_config.Output));
        return Task.CompletedTask;
    }

    private void CopyDirectory(DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory, bool recursive = true)
    {
        if (!sourceDirectory.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: '{sourceDirectory.FullName}'");

        var files = sourceDirectory.GetFiles();
        var subDirectories = sourceDirectory.GetDirectories();

        if (!destinationDirectory.Exists)
            Directory.CreateDirectory(destinationDirectory.FullName);

        foreach (var file in files)
            file.CopyTo(Path.Combine(destinationDirectory.FullName, file.Name));

        if (recursive)
        {
            foreach (var subDirectory in subDirectories)
                CopyDirectory(subDirectory, new(Path.Combine(destinationDirectory.FullName, subDirectory.Name)), recursive);
        }
    }
}

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
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
    }
}

public record class GitHubAsset(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("browser_download_url")]
    Uri DownloadUrl
);

public record class GitHubRelease(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("tag_name")]
    string TagName,
    [property: JsonPropertyName("prerelease")]
    bool IsPreRelease,
    [property: JsonPropertyName("assets")]
    ICollection<GitHubAsset> Assets
);

public static class GitHubApiClient
{
    private static readonly Uri BaseAddress = new("https://api.github.com");

    public static async Task<ICollection<GitHubRelease>> GetReleasesAsync(string owner, string repo, int page = 1, int pageSize = 30)
    {
        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync($"/repos/{owner}/{repo}/releases?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ICollection<GitHubRelease>>())!;
    }

    public static async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo)
    {
        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync($"/repos/{owner}/{repo}/releases/latest");
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        return (await response.Content.ReadFromJsonAsync<GitHubRelease>())!;
    }

    public static async Task<GitHubRelease> GetReleaseByTagAsync(string owner, string repo, string tag)
    {
        using var httpClient = CreateHttpClient();
        var response = await httpClient.GetAsync($"/repos/{owner}/{repo}/releases/tags/{tag}");
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        return (await response.Content.ReadFromJsonAsync<GitHubRelease>())!;
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient { BaseAddress = BaseAddress };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new(nameof(Gpfm), null));
        return httpClient;
    }
}
