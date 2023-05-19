using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Gpfm.Cli;

[JsonPolymorphic(TypeDiscriminatorPropertyName = TypeDiscriminatorPropertyName)]
[JsonDerivedType(typeof(GitHubSource), GitHubSource.TypeDiscriminator)]
[JsonDerivedType(typeof(UrlSource), UrlSource.TypeDiscriminator)]
public record class Source(
    [property: JsonPropertyName("name")]
    string Name
)
{
    public const string TypeDiscriminatorPropertyName = "type";
};

public record class GitHubSource(
    string Name,
    [property: JsonPropertyName("repository")]
    Uri Repository,
    [property: JsonPropertyName("includePrerelease")]
    bool IncludePrerelease,
    [property: JsonPropertyName("tag")]
    string? Tag,
    [property: JsonPropertyName("asset")]
    string Asset
) : Source(Name)
{
    public const string TypeDiscriminator = "gitHub";
};

public record class UrlSource(
    string Name,
    [property: JsonPropertyName("url")]
    Uri Url
) : Source(Name)
{
    public const string TypeDiscriminator = "url";
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

        foreach (var source in _config.Sources)
        {
            switch (source)
            {
                case GitHubSource gitHubSource:
                    await ProcessSourceAsync(gitHubSource);
                    break;
                case UrlSource urlSource:
                    await ProcessSourceAsync(urlSource);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected source type '{source.GetType()}'");
            }
        }
    }

    private async Task ProcessSourceAsync(GitHubSource source)
    {
        var path = source.Repository.LocalPath.Trim('/').Split('/');
        if (path.Length != 2 || !path.All(p => !string.IsNullOrWhiteSpace(p)))
            throw new ArgumentException($"Invalid GitHub repository '{source.Repository}'");

        var (owner, repo) = (path[0], path[1]);

        GitHubRelease? release;

        if (source.Tag != null)
            release = await GitHubApiClient.GetReleaseByTagAsync(owner, repo, source.Tag);
        else if (source.IncludePrerelease)
            release = await GitHubApiClient.GetLatestReleaseAsync(owner, repo);
        else
            release = (await GitHubApiClient.GetReleasesAsync(owner, repo))
                .First(r => !r.IsPreRelease);

        var asset = release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name, source.Asset))
            ?? throw new InvalidOperationException($"No asset found matching pattern '{source.Asset}'");

        var stagingDirectory = new DirectoryInfo(Path.Join(_tempDirectory, source.Name));
        if (!stagingDirectory.Exists) stagingDirectory.Create();

        var stagingFile = new FileInfo(Path.Join(stagingDirectory.FullName, asset.Name));

        using (var httpClient = new HttpClient())
        using (var fileDownloadStream = await httpClient.GetStreamAsync(asset.DownloadUrl))
        using (var stagingFileStream = stagingFile.OpenWrite())
            await fileDownloadStream.CopyToAsync(stagingFileStream);

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

        // TODO better infer filename from Content-Disposition

        using (var httpClient = new HttpClient())
        using (var fileDownloadStream = await httpClient.GetStreamAsync(source.Url))
        using (var stagingFileStream = stagingFile.OpenWrite())
            await fileDownloadStream.CopyToAsync(stagingFileStream);

        var extractDirectory = Path.Join(stagingDirectory.FullName, Path.ChangeExtension(stagingFile.Name, null));
        ZipFile.ExtractToDirectory(stagingFile.FullName, extractDirectory, overwriteFiles: true);
    }
}

internal class Program
{
    private static async Task Main(string[] args)
    {
        var configJson = await File.ReadAllTextAsync("config.json");
        var config = JsonSerializer.Deserialize<JobConfig>(configJson)
            ?? throw new JsonException("Deserialized object instance is null");

        await Job.RunAsync(config);
    }
}

public record class GitHubAsset(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("browser_download_url")]
    Uri DownloadUrl
);

public record class GitHubRelease(
    [property: JsonPropertyName("prerelease")]
    bool IsPreRelease,
    [property: JsonPropertyName("tag_name")]
    string TagName,
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
