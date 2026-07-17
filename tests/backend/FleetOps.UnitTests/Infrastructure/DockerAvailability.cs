using System.Diagnostics;

namespace FleetOps.UnitTests.Infrastructure;

internal static class DockerAvailability
{
    private static readonly Lazy<(bool IsAvailable, string Reason)> Probe = new(ProbeDocker);

    public static bool IsAvailable => Probe.Value.IsAvailable;

    public static string Reason => Probe.Value.Reason;

    private static (bool IsAvailable, string Reason) ProbeDocker()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "version --format {{.Server.Os}}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit(10_000);

            if (process.ExitCode != 0)
            {
                return (false, string.IsNullOrWhiteSpace(error) ? "docker version failed." : error);
            }

            if (!output.Contains("linux", StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Docker server '{output}' is not using a Linux engine.");
            }

            return (true, "Docker Linux engine available.");
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }
}
