﻿using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    [property: JsonPropertyName("uri")]
    Uri Uri
) : Source(Name);

public record class Config(
    [property: JsonPropertyName("sources")]
    ICollection<Source> Sources
);

internal class Program
{
    private static async Task Main(string[] args)
    {
        var configJson = await File.ReadAllTextAsync("config.json");
        var config = JsonSerializer.Deserialize<Config>(configJson)
            ?? throw new JsonException("Deserialized object instance is null");

        foreach (var source in config.Sources)
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

    public static async Task ProcessSourceAsync(GitHubSource source)
    {
        var path = source.Repository.LocalPath.Trim('/').Split('/');
        if (path.Length != 2) throw new ArgumentException($"Invalid GitHub repository '{source.Repository}'", nameof(source));
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
    }

    public static async Task ProcessSourceAsync(UriSource source)
    {

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
