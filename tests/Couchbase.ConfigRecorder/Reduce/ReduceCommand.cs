using System.Text;
using System.Text.RegularExpressions;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// Turns run folders of one cluster type into a small anonymized profile in &lt;out&gt;/&lt;name&gt;.
/// It writes to a temporary folder first and scans it for original host names and UUIDs.
/// The profile folder is replaced only when the scan finds nothing.
/// </summary>
internal static partial class ReduceCommand
{
    public static int Run(string name, string outFolder, string? serverVersion, IReadOnlyList<string> runFolders, TextWriter output)
    {
        if (!ProfileName().IsMatch(name))
        {
            Console.Error.WriteLine($"The profile name '{name}' must be lower case letters, digits and dashes.");
            return 2;
        }

        foreach (var folder in runFolders)
        {
            if (!File.Exists(RunFolder.EventsPath(folder)))
            {
                Console.Error.WriteLine($"No events.jsonl in '{folder}'.");
                return 2;
            }
        }

        var result = new ProfileBuilder(name, serverVersion, runFolders.Select(RunFolder.Load)).Build();
        foreach (var warning in result.Warnings)
        {
            Console.Error.WriteLine(warning);
        }

        var staging = Path.Combine(Path.GetTempPath(), $"config-recorder-reduce-{name}-{Environment.ProcessId}");
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(Path.Combine(staging, "examples"));

        JsonText.WriteFile(Path.Combine(staging, "profile.json"), result.Profile);
        File.WriteAllText(Path.Combine(staging, "profile.md"), ProfileReport.Write(result.Profile), new UTF8Encoding(false));
        foreach (var example in result.Examples)
        {
            JsonText.WriteFile(Path.Combine(staging, example.File), example.Body);
        }

        var leaks = Scan(staging, result.Fragments);
        if (leaks.Count > 0)
        {
            foreach (var leak in leaks)
            {
                Console.Error.WriteLine(leak);
            }

            Console.Error.WriteLine($"The profile holds original values. It stays in {staging} for a look, and {outFolder} is not changed.");
            return 1;
        }

        var target = Path.Combine(outFolder, name);
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        Directory.CreateDirectory(Path.Combine(target, "examples"));
        long total = 0;
        foreach (var file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(staging, file);
            File.Copy(file, Path.Combine(target, relative));
            var size = new FileInfo(file).Length;
            total += size;
            output.WriteLine($"{size,9}  {Path.Combine(target, relative)}");
        }

        Directory.Delete(staging, recursive: true);
        output.WriteLine($"{total,9}  bytes in total. The scan found none of {result.Fragments.Count} original values.");
        return 0;
    }

    private static List<string> Scan(string folder, IReadOnlyList<string> fragments)
    {
        var leaks = new List<string>();
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file);
            foreach (var fragment in fragments)
            {
                if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    leaks.Add($"{Path.GetRelativePath(folder, file)} holds '{fragment}'.");
                }
            }
        }

        return leaks;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex ProfileName();
}
