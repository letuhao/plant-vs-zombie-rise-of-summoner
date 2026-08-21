using System.Text.Json;
using FusionRpg.Injector.Host;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// Presentation-only overlay settings (shield bar, hotkeys). Not a cheats SoT —
/// gameplay cheats stay on the web document.
/// </summary>
public static class OverlaySettings
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static string _path = "";
    static State _state = State.Defaults();

    public static bool ShieldBarEnabled
    {
        get => _state.ShieldBarEnabled;
        set
        {
            if (_state.ShieldBarEnabled == value) return;
            _state.ShieldBarEnabled = value;
            Save();
        }
    }

    public static string ShieldBarHotKey
    {
        get => _state.ShieldBarHotKey;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "F9" : value.Trim();
            if (string.Equals(_state.ShieldBarHotKey, v, StringComparison.OrdinalIgnoreCase)) return;
            _state.ShieldBarHotKey = v;
            Save();
        }
    }

    public static string SettingsHotKey
    {
        get => _state.SettingsHotKey;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "F7" : value.Trim();
            if (string.Equals(_state.SettingsHotKey, v, StringComparison.OrdinalIgnoreCase)) return;
            _state.SettingsHotKey = v;
            Save();
        }
    }

    public static bool SettingsOpen { get; set; }

    public static void Init(string pluginDir)
    {
        _path = Path.Combine(pluginDir ?? "", "overlay-settings.json");
        _state = State.Defaults();
        TryLoad();
    }

    public static void ToggleShieldBar() => ShieldBarEnabled = !ShieldBarEnabled;

    public static void ToggleSettingsOpen() => SettingsOpen = !SettingsOpen;

    static void TryLoad()
    {
        try
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<State>(json, JsonOpts);
            if (loaded == null) return;
            _state.ShieldBarEnabled = loaded.ShieldBarEnabled;
            if (!string.IsNullOrWhiteSpace(loaded.ShieldBarHotKey))
                _state.ShieldBarHotKey = loaded.ShieldBarHotKey.Trim();
            if (!string.IsNullOrWhiteSpace(loaded.SettingsHotKey))
                _state.SettingsHotKey = loaded.SettingsHotKey.Trim();
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("overlay settings load: " + ex.Message); } catch { }
        }
    }

    static void Save()
    {
        try
        {
            if (string.IsNullOrEmpty(_path)) return;
            var json = JsonSerializer.Serialize(_state, JsonOpts);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("overlay settings save: " + ex.Message); } catch { }
        }
    }

    sealed class State
    {
        public bool ShieldBarEnabled { get; set; } = true;
        public string ShieldBarHotKey { get; set; } = "F9";
        public string SettingsHotKey { get; set; } = "F7";

        public static State Defaults() => new();
    }
}
