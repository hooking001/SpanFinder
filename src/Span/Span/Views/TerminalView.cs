using System;
using System.Text.Json;
using System.Threading.Tasks;
using EasyWindowsTerminalControl;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Span.Views;

// ConPTY provides the real interactive shell; xterm renders VT sequences locally.
public sealed class TerminalView : Grid, IDisposable
{
    private readonly WebView2 _web = new();
    private readonly TermPTY _session = new();
    private readonly string _path;
    private readonly string _command;
    private readonly Helpers.WindowsTerminalSettings _settings;
    private string? _backgroundUrl;
    private bool _started;
    private bool _disposed;

    public TerminalView(string path)
    {
        _path = path;
        _settings = Helpers.WindowsTerminalSettings.Load();
        _command = _settings.CommandLine;
        Children.Add(_web);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            await _web.EnsureCoreWebView2Async();
            if (_disposed) return;
            var core = _web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.SetVirtualHostNameToFolderMapping("terminal.span.local",
                System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal"),
                CoreWebView2HostResourceAccessKind.DenyCors);
            if (_settings.BackgroundImage is string image && System.IO.File.Exists(image))
            {
                core.SetVirtualHostNameToFolderMapping("terminal-background.span.local", System.IO.Path.GetDirectoryName(image)!, CoreWebView2HostResourceAccessKind.DenyCors);
                _backgroundUrl = "https://terminal-background.span.local/" + Uri.EscapeDataString(System.IO.Path.GetFileName(image));
            }
            core.NavigationStarting += (_, args) =>
            {
                if (!args.Uri.StartsWith("https://terminal.span.local/", StringComparison.Ordinal)) args.Cancel = true;
            };
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.WebMessageReceived += OnMessage;
            _session.TerminalOutput += (_, output) => DispatcherQueue.TryEnqueue(() =>
            {
                if (!_disposed) core.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "output", data = output.Data }));
            });
            _session.TermReady += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                if (_disposed) { _session.StopExternalTermOnly(); return; }
                core.PostWebMessageAsJson("{\"type\":\"ready\"}");
            });
            core.Navigate("https://terminal.span.local/index.html");
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void OnMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (_disposed || !args.Source.StartsWith("https://terminal.span.local/", StringComparison.Ordinal)) return;
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            var root = doc.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "configure":
                    sender.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "configure", options = _settings.Options,
                        backgroundImage = _backgroundUrl, backgroundOpacity = _settings.BackgroundOpacity,
                        backgroundSize = _settings.BackgroundSize, padding = _settings.Padding }));
                    break;
                case "size":
                    int cols = Math.Clamp(root.GetProperty("cols").GetInt32(), 2, 500);
                    int rows = Math.Clamp(root.GetProperty("rows").GetInt32(), 2, 300);
                    if (!_started)
                    {
                        _started = true;
                        _ = Task.Run(() =>
                        {
                            try { _session.Start(_command, cols, rows, false, null, _path); }
                            catch (Exception ex) { DispatcherQueue.TryEnqueue(() => ReportError(ex)); }
                        });
                    }
                    else if (_session.TermProcIsStarted) _session.Resize(cols, rows);
                    break;
                case "input":
                    if (_session.TermProcIsStarted) _session.WriteToTerm(root.GetProperty("data").GetString().AsSpan());
                    break;
            }
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void ReportError(Exception ex)
    {
        Helpers.DebugLogger.Log("[Terminal] " + ex);
        if (!_disposed && _web.CoreWebView2 != null)
            _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "output", data = "\r\n" + ex.Message + "\r\n" }));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Loaded -= OnLoaded;
        try { _session.CloseStdinToApp(); } catch { }
        try { _session.StopExternalTermOnly(); } catch { }
        _web.Close();
        Children.Clear();
    }
}
