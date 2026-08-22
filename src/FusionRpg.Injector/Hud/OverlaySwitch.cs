using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using FusionRpg.Core.Overlay;
using FusionRpg.Injector.Host;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// Sends the in-game button's toggle to whichever process hosts the web overlay window.
/// Decisions live in <see cref="OverlaySwitchState"/>; this class is only I/O.
///
/// The pipe work runs on the thread pool — this is the injector's only background work, so it
/// swallows everything and never touches a Unity API. Results come back through two volatile
/// flags and are applied on the Unity thread in <see cref="Tick"/>, keeping the state single-threaded.
/// </summary>
public static class OverlaySwitch
{
    const string PipeName = "FusionRpg.Overlay";
    const int ConnectTimeoutMs = 250;

    static readonly OverlaySwitchState State = new();
    static readonly Stopwatch Clock = Stopwatch.StartNew();

    static volatile bool _clickPending;
    static volatile bool _probeInFlight;
    static volatile bool _sendDone;
    static volatile bool _sendOk;
    static volatile bool _probeDone;
    static volatile bool _probeOk;
    static bool _viewStarted;

    /// <summary>Read by the GUI each pass — must stay allocation-free.</summary>
    public static bool ButtonVisible => State.ButtonVisible;

    /// <summary>Called from OnGUI. Sets a flag only: no pipe, no log, no Unity work on the click.</summary>
    public static void RequestToggle() => _clickPending = true;

    /// <summary>The launcher may have started or stopped between matches — re-probe.</summary>
    public static void OnMatchStart() => State.OnMatchStart();

    /// <summary>Board over — the button is in-match chrome, so it goes away with the board.</summary>
    public static void OnMatchEnd()
    {
        State.OnMatchEnd();
        // Leaving the lawn should not leave the web UI covering the menu.
        if (InjectorHosted && OverlayViewHost.IsVisible) OverlayViewHost.Hide();
    }

    static bool InjectorHosted =>
        RpgHost.OverlayHost == FusionRpg.Core.Overlay.OverlayHostMode.Injector;

    /// <summary>Per-frame, Unity thread. Cheap: two flag reads and an integer compare when idle.</summary>
    public static void Tick()
    {
        var now = Clock.ElapsedMilliseconds;

        if (InjectorHosted)
        {
            TickInjectorHosted(now);
            return;
        }

        // Results are produced off-thread and applied here, so the state object stays single-threaded.
        if (_sendDone)
        {
            _sendDone = false;
            State.MarkSendComplete();
            ApplyReachability(_sendOk, now); // a failed toggle is also an answer about the host
        }
        if (_probeDone)
        {
            _probeDone = false;
            ApplyReachability(_probeOk, now);
        }

        State.SettingsEnabled = OverlaySettings.OverlayButtonEnabled;

        if (_clickPending)
        {
            _clickPending = false;
            if (State.TryClick(now))
                Send("toggle", isProbe: false);
        }

        if (!_probeInFlight && State.ShouldProbe(now))
        {
            State.MarkProbeSent(now);
            Send("ping", isProbe: true);
        }
    }

    /// <summary>
    /// overlayHost=injector: the view lives in this process, so there is no pipe, no probe and no
    /// debounce worth keeping — a toggle is a queue push. Reachability becomes "the view came up".
    /// </summary>
    static void TickInjectorHosted(long now)
    {
        State.SettingsEnabled = OverlaySettings.OverlayButtonEnabled;

        if (!_viewStarted)
        {
            _viewStarted = true;
            OverlayViewHost.Start(RpgHost.ServerUrl);
        }

        if (State.ApplyProbeResult(OverlayViewHost.Available, now))
        {
            RpgHost.Log.Info(State.HostReachable
                ? "In-game overlay view ready — button enabled."
                : "In-game overlay view unavailable — hiding the button.");
        }

        if (!_clickPending) return;
        _clickPending = false;
        if (State.TryClick(now))
        {
            OverlayViewHost.Toggle();
            State.MarkSendComplete(); // no wire, so the send is over as soon as it is queued
        }
    }

    /// <summary>Game is quitting — tear the view down so no browser process outlives us.</summary>
    public static void Shutdown()
    {
        if (_viewStarted) OverlayViewHost.Shutdown();
    }

    /// <summary>Logs only when reachability flips, so an absent launcher costs one line, not one per probe.</summary>
    static void ApplyReachability(bool reachable, long nowMs)
    {
        if (!State.ApplyProbeResult(reachable, nowMs)) return;
        RpgHost.Log.Info(State.HostReachable
            ? "Overlay host found — in-game overlay button enabled."
            : "Overlay host gone — hiding the in-game overlay button.");
    }

    static void Send(string verb, bool isProbe)
    {
        if (isProbe) _probeInFlight = true;

        _ = Task.Run(() =>
        {
            var ok = false;
            try { ok = TrySend(verb); }
            finally
            {
                // Always clear the in-flight gate, or one hiccup wedges the button forever.
                if (isProbe)
                {
                    _probeInFlight = false;
                    _probeOk = ok;
                    _probeDone = true;
                }
                else
                {
                    _sendOk = ok;
                    _sendDone = true;
                }
            }
        });
    }

    static bool TrySend(string verb)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(ConnectTimeoutMs);
            var bytes = Encoding.ASCII.GetBytes(verb + "\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
            return true;
        }
        catch
        {
            return false; // no host listening is the normal case, not an error worth logging per attempt
        }
    }
}
