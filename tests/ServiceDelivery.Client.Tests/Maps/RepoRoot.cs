using System;
using System.IO;

namespace ServiceDelivery.Client.Tests.Maps;

/// <summary>
/// Locates the frontend repo root from a running test (FE-025 Config-AC tests). The test binary runs out
/// of <c>tests/ServiceDelivery.Client.Tests/bin/...</c>; this walks parent directories until it finds the
/// solution file (<c>ServiceDelivery.Client.slnx</c>), giving committed files (the placeholder
/// <c>appsettings.json</c>, the maps key doc, <c>.gitignore</c>, the JS module) a stable absolute path to
/// assert against regardless of where the test host is launched from.
/// </summary>
internal static class RepoRoot
{
    private const string SolutionMarker = "ServiceDelivery.Client.slnx";

    public static string Path()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, SolutionMarker)))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException(
                $"Could not locate '{SolutionMarker}' walking up from '{AppContext.BaseDirectory}'.");
        }

        return dir.FullName;
    }

    public static string Combine(params string[] segments)
    {
        var all = new string[segments.Length + 1];
        all[0] = Path();
        Array.Copy(segments, 0, all, 1, segments.Length);
        return System.IO.Path.Combine(all);
    }
}
