using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FusionRpg.Launcher.Services;
using Microsoft.Web.WebView2.Core;

namespace FusionRpg.Launcher;

/// <summary>
/// Borderless topmost WebView2 window that covers the game window and shows the RPG web UI.
/// Hidden (never destroyed) on toggle so the SPA keeps its SignalR session between switches.
/// </summary>
public partial class OverlayWindow : Window
{
    readonly Action _backToGame;
    bool _webReady;
    bool _allowClose;
    string? _currentUrl;

    public OverlayWindow(Action backToGame, string hotKeyName)
    {
        InitializeComponent();
        _backToGame = backToGame;
        BackButton.Content = $"⟵ Back to game ({hotKeyName} / Esc)";
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _backToGame();
            }
        };
    }

    /// <summary>Show the overlay covering the game window (fallback: maximized) and load the UI.</summary>
    public async Task ShowOverGameAsync(string url)
    {
        if (GameWindowInterop.TryGetGameWindowRect(out var rect))
        {
            WindowState = WindowState.Normal;
            Show();
            var hwnd = new WindowInteropHelper(this).Handle;
            GameWindowInterop.MoveWindowOver(hwnd, rect);
        }
        else
        {
            Show();
            WindowState = WindowState.Maximized;
        }

        Activate();
        await EnsureWebAsync(url);
        if (_webReady)
            Web.Focus();
    }

    async Task EnsureWebAsync(string url)
    {
        if (!_webReady)
        {
            try
            {
                // User-data folder must be writable — never next to the exe (Program Files).
                var dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FusionRpg", "webview2");
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: dataDir);
                await Web.EnsureCoreWebView2Async(env);
                Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webReady = true;
            }
            catch (Exception ex)
            {
                FallbackText.Text =
                    "The Microsoft Edge WebView2 Runtime is not available, so the overlay cannot render the web UI.\n\n" +
                    "Install it from:\nhttps://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                    "Until then, use “Open RPG UI” in the launcher to open the UI in your normal browser.\n\n" +
                    "Detail: " + ex.Message;
                FallbackText.Visibility = Visibility.Visible;
                return;
            }
        }

        if (!string.Equals(_currentUrl, url, StringComparison.OrdinalIgnoreCase))
        {
            _currentUrl = url;
            Web.CoreWebView2.Navigate(url);
        }
    }

    void Back_Click(object sender, RoutedEventArgs e) => _backToGame();

    /// <summary>Alt+F4 etc. hides instead of destroying, so toggling stays instant.</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            _backToGame();
            return;
        }
        base.OnClosing(e);
    }

    /// <summary>Really close (launcher shutdown).</summary>
    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }
}
