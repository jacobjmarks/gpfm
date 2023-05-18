using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Gpfm.Cli;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GitHubSource), typeDiscriminator: "gitHub")]
[JsonDerivedType(typeof(UriSource), typeDiscriminator: "uri")]
public record class Source(
    [property: JsonPropertyName("name")]
    string Name
);

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
) : Source(Name);

public record class UriSource(
    string Name,
    [property: JsonPropertyName("url")]
    Uri Url
) : Source(Name);

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
                case UriSource uriSource:
                    await ProcessSourceAsync(uriSource);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected source type");
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
        {
            release = await GitHubApiClient.GetReleaseByTagAsync(owner, repo, source.Tag);
        }
        else
        {
            if (source.IncludePrerelease)
            {
                var releases = await GitHubApiClient.GetReleasesAsync(owner, repo);
                release = releases.First(r => !r.IsPreRelease);
            }
            else
            {
                release = await GitHubApiClient.GetLatestReleaseAsync(owner, repo);
            }
        }

        var asset = release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name, source.Asset))
            ?? throw new InvalidOperationException($"No asset found matching pattern '{source.Asset}'");

        var stagingDirectory = Path.Join(_tempDirectory, source.Name);
        Directory.CreateDirectory(stagingDirectory);
        var stagingFile = Path.Join(stagingDirectory, asset.Name);
        await DownloadFileAsync(asset.DownloadUrl, stagingFile);

        var extractDirectory = _config.Output;
        ZipFile.ExtractToDirectory(stagingFile, extractDirectory);
    }

    private async Task ProcessSourceAsync(UriSource source)
    {
        var stagingDirectory = Path.Join(_tempDirectory, source.Name);
        Directory.CreateDirectory(stagingDirectory);
        var stagingFile = Path.Join(stagingDirectory, source.Url.Segments.Last());
        await DownloadFileAsync(source.Url.ToString(), stagingFile);

        try
        {
            using var _ = ZipFile.Open(stagingFile, ZipArchiveMode.Read);
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Could not open zip archive from URL: '{source.Url}'", e);
        }

        var extractDirectory = Path.Join(stagingDirectory, Path.ChangeExtension(Path.GetFileName(stagingFile), null));
        ZipFile.ExtractToDirectory(stagingFile, extractDirectory);
    }

    private static async Task DownloadFileAsync(string uri, string path)
    {
        using var fileStream = File.Create(path);
        using var httpClient = new HttpClient();
        var dataStream = await httpClient.GetStreamAsync(uri);
        await dataStream.CopyToAsync(fileStream);
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
    string DownloadUrl
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
