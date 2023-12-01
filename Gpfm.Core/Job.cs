namespace Gpfm.Core;

public record JobStep(string? Name, bool Skip, string Source);
public record JobConfig(IEnumerable<JobStep> Steps, string Output);

public class Job(JobConfig config)
{
    private readonly JobConfig _config = config;

    public static async Task RunAsync(JobConfig config, CancellationToken cancellationToken = default)
    {
        var job = new Job(config);
        await job.RunAsync(cancellationToken);
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigurationIsValid(_config);

        if (Directory.Exists(_config.Output))
            Directory.Delete(_config.Output, recursive: true);
        Directory.CreateDirectory(_config.Output);

        var i = 0;
        foreach (var step in _config.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"[{i + 1:D2}] Applying {step}");

            if (step.Skip)
            {
                Console.WriteLine("Skipping ...");
                i++;
                continue;
            }

            CopyDirectory(new(step.Source), new(_config.Output), recursive: true, cancellationToken);

            i++;
        }

        return Task.CompletedTask;
    }

    private static void EnsureConfigurationIsValid(JobConfig config)
    {
        foreach (var step in config.Steps)
        {
            if (step.Skip)
                continue;

            var directory = new DirectoryInfo(step.Source);
            if (!directory.Exists)
                throw new DirectoryNotFoundException($"Directory not found: '{directory.FullName}'");
        }
    }

    private static void CopyDirectory(
        DirectoryInfo directory,
        DirectoryInfo destination,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        if (!directory.Exists)
            throw new DirectoryNotFoundException($"Directory not found: '{directory.FullName}'");

        var files = directory.GetFiles();
        var subdirs = directory.GetDirectories();

        if (!destination.Exists)
            Directory.CreateDirectory(destination.FullName);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destFile = new FileInfo(Path.Combine(destination.FullName, file.Name));
            if (destFile.Exists)
                Console.WriteLine($"Warning: Overwriting file '{Path.GetRelativePath(destination.FullName, destFile.FullName)}' ...");
            file.CopyTo(destFile.FullName, overwrite: true);
        }

        if (recursive)
        {
            foreach (var subdir in subdirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopyDirectory(subdir, new(Path.Combine(destination.FullName, subdir.Name)), recursive, cancellationToken);
            }
        }
    }
}
