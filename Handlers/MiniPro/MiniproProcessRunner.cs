// Real IMiniproRunner: spawns the minipro binary with UseShellExecute=false and
// redirected stdout/stderr, streams output lines, and is fully cancellable (a
// 65,536-vector PLA run is long). Models TabConfiguration.TryStartCommandWith
// Diagnostics (ArgumentList, no shell string) + ScopeScpiClient (streaming/CT),
// but streams rather than ReadToEnd so progress is live.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Handlers.IcTesting;

public sealed class MiniproProcessRunner : IMiniproRunner
{
    private readonly string? _explicitPath;

    /// <param name="explicitPath">Optional override (e.g. UserSettings.MiniproPathOverride).</param>
    public MiniproProcessRunner(string? explicitPath = null) => _explicitPath = explicitPath;

    public string? ResolveBinary()
    {
        if (!string.IsNullOrWhiteSpace(_explicitPath) && File.Exists(_explicitPath))
            return _explicitPath;
        if (OperatingSystem.IsWindows())
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, BinaryName);
            if (File.Exists(bundled))
                return bundled;
        }
        return BinaryName;
    }

    private static string BinaryName => OperatingSystem.IsWindows() ? "minipro.exe" : "minipro";

    public async Task<MiniproRunResult> RunAsync(
        IReadOnlyList<string> args, IProgress<string>? output, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveBinary(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Redirect stdin so we can close it: minipro prompts on stdin in some
            // cases (e.g. "Which database…?"), and with an inherited/blocking stdin a
            // GUI-spawned minipro would hang forever waiting for input.
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdout.AppendLine(e.Data);
            output?.Report(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        try
        {
            if (!proc.Start())
                return MiniproRunResult.NotStarted("minipro failed to start");
        }
        catch (Exception ex)
        {
            // Most commonly: binary missing, or (on Linux) a USB-permission/udev failure.
            return MiniproRunResult.NotStarted(ex.Message);
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        // Send EOF immediately: minipro never needs input here, and an open stdin
        // lets any prompt block forever. EOF makes a prompt return at once (it aborts
        // rather than waits), so the test can never hang on a question.
        try { proc.StandardInput.Close(); } catch { /* nothing to close */ }

        // Backstop timeout so a stalled device read fails cleanly instead of spinning.
        // Generous enough for the full 65,536-vector PLA run (~2 min); the Cancel
        // button covers impatience.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* already gone */ }
            if (ct.IsCancellationRequested) throw;   // the user pressed Cancel
            // Our own timeout fired — return what we captured rather than hang.
            return new MiniproRunResult(true, -1, stdout.ToString(),
                (stderr + "\nminipro did not respond within 5 minutes — aborted.").Trim());
        }

        return new MiniproRunResult(true, proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
