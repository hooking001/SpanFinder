using System;
using Microsoft.UI.Xaml;
using EasyWindowsTerminalControl;

namespace Span;

public sealed partial class MainWindow
{
    private Views.TerminalView? _embeddedTerminal;
    private bool _terminalEnabledSplit;
    private GridLength _terminalPreviousRightWidth;

    private void OpenTerminalPane(string path)
    {
        if (!System.IO.Directory.Exists(path))
        {
            ViewModel.ShowToast(_loc.Get("Error_TerminalInvalidPath"));
            return;
        }
        CloseTerminalPane();
        try
        {
            _terminalEnabledSplit = !ViewModel.IsSplitViewEnabled;
            _terminalPreviousRightWidth = RightPaneCol.Width;
            ViewModel.IsSplitViewEnabled = true;
            RightPaneCol.Width = new GridLength(1, GridUnitType.Star);
            UpdateSplitViewButtonState();
            ViewModel.ActivePane = Models.ActivePane.Left;
            Helpers.DebugLogger.Log("[Terminal] Creating control");
            _embeddedTerminal = new Views.TerminalView(path);
            Helpers.DebugLogger.Log("[Terminal] Control created");
            TerminalPathLabel.Text = "PowerShell — " + path;
            TerminalContent.Content = _embeddedTerminal;
            TerminalPaneHost.Visibility = Visibility.Visible;
            Helpers.DebugLogger.Log("[Terminal] Pane visible");
            // The terminal page focuses its input after the shell is ready.
        }
        catch (Exception ex)
        {
            CloseTerminalPane();
            Helpers.DebugLogger.Log("[Terminal] " + ex);
            ViewModel.ShowError(ex.Message);
        }
    }

    private void OnCloseTerminal(object sender, RoutedEventArgs e) => CloseTerminalPane();

    private void CloseTerminalPane()
    {
        if (_embeddedTerminal == null) return;
        var terminal = _embeddedTerminal;
        _embeddedTerminal = null;
        if (terminal != null)
        {
            terminal.Dispose();
        }
        TerminalContent.Content = null;
        TerminalPaneHost.Visibility = Visibility.Collapsed;
        if (_terminalEnabledSplit) ViewModel.IsSplitViewEnabled = false;
        RightPaneCol.Width = _terminalPreviousRightWidth;
        UpdateSplitViewButtonState();
        _terminalEnabledSplit = false;
    }
}