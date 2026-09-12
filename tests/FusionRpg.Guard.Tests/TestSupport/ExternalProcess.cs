using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Correct stdout/stderr draining for a redirected child process. Copied verbatim from
/// <c>tests/FusionRpg.Core.Tests/TestSupport/ExternalProcess.cs</c> rather than referenced, since
/// test projects in this repo do not reference each other.
///
/// <para><b>The bug this exists to kill:</b> <c>Process.StandardOutput.ReadToEnd()</c> immediately
/// followed by <c>Process.StandardError.ReadToEnd()</c> is a documented deadlock hazard: if the child
/// writes enough to stderr to fill the OS pipe buffer while the parent is still blocked draining
/// stdout to EOF, the child stalls and the parent never reaches EOF either. A later
/// <c>WaitForExit(60_000)</c> can never fire because the code is blocked on the read before it —
/// which is exactly why a hung guard-script test produced no output, no exit code, and no report
/// (observed: a local run blocking for 1h30). This helper drains both streams CONCURRENTLY and, on
/// timeout, kills the whole process tree and fails instead of hanging forever.</para>
/// </summary>
public static class ExternalProcess
{
    /// <summary>
    /// Starts <paramref name="psi"/> (this method owns stream redirection — do not rely on the
    /// caller's <c>RedirectStandardOutput</c>/<c>RedirectStandardError</c>) and drains stdout/stderr
    /// concurrently. On timeout, kills the whole process tree and fails with
    /// <paramref name="timeoutMessage"/>.
    /// </summary>
    public static (int Exit, string Stdout, string Stderr) Run(ProcessStartInfo psi, int timeoutMs, string timeoutMessage)
    {
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;

        using var p = Process.Start(psi)!;
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        var exited = p.WaitForExit(timeoutMs);
        if (!exited)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* best-effort — we are already failing */ }
            Assert.Fail(timeoutMessage);
        }

        // The process has exited and closed its pipes, so these should already be done or finish
        // immediately — bounded defensively rather than trusted blindly, since a still-alive
        // grandchild could in principle hold a duplicated handle open.
        Task.WaitAll(new Task[] { stdoutTask, stderrTask }, TimeSpan.FromSeconds(10));

        return (
            p.ExitCode,
            stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "",
            stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "");
    }
}
