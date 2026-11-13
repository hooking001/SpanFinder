using Span.Helpers;

namespace Span.Tests.Helpers;

[TestClass]
public class WindowsTerminalSettingsTests
{
    private const string Schemes = """[{"name":"Campbell","background":"#0c0c0c","foreground":"#cccccc","purple":"#881798","brightPurple":"#b4009e","cursorColor":"#ffffff"}]""";

    [TestMethod]
    public void InheritsDefaultsAndMergesFontOverrides()
    {
        var settings = WindowsTerminalSettings.Parse("""
        { // JSONC with a trailing comma is valid in Windows Terminal.
          "defaultProfile":"ps", "profiles":{"defaults":{"font":{"face":"JetBrainsMono Nerd Font","size":12},"cursorColor":"#16C60C"},
          "list":[{"guid":"ps","name":"Windows PowerShell","commandline":"powershell.exe","font":{"size":15}},]},}
        """, Schemes);
        Assert.AreEqual("JetBrainsMono Nerd Font, monospace", settings.Options["fontFamily"]!.GetValue<string>());
        Assert.AreEqual(20d, settings.Options["fontSize"]!.GetValue<double>());
        Assert.AreEqual("#16C60C", settings.Options["theme"]!["cursor"]!.GetValue<string>());
        Assert.AreEqual("powershell.exe", settings.CommandLine);
        Assert.IsFalse(settings.CommandLine.Contains("-NoProfile"));
    }

    [TestMethod]
    public void DefaultPowerShellProfileWinsOverFirstPowerShell()
    {
        var settings = WindowsTerminalSettings.Parse("""
        {"defaultProfile":"ps7","profiles":{"list":[
        {"guid":"ps5","name":"Windows PowerShell","commandline":"powershell.exe"},
        {"guid":"ps7","name":"PowerShell","commandline":"pwsh.exe -NoLogo"}]}}
        """, Schemes);
        Assert.AreEqual("pwsh.exe -NoLogo", settings.CommandLine);
    }

    [TestMethod]
    public void NonPowerShellDefaultDoesNotChangeShellToCmd()
    {
        var settings = WindowsTerminalSettings.Parse("""
        {"defaultProfile":"cmd","profiles":{"list":[
        {"guid":"cmd","name":"cmd","commandline":"cmd.exe"},
        {"guid":"ps","name":"PowerShell","commandline":"pwsh.exe"}]}}
        """, Schemes);
        Assert.AreEqual("pwsh.exe", settings.CommandLine);
    }

    [TestMethod]
    public void CustomSchemeAndProfileColorsOverrideBuiltinScheme()
    {
        var settings = WindowsTerminalSettings.Parse("""
        {"profiles":{"defaults":{"foreground":"#123456","cursorShape":"bar"}},
        "schemes":[{"name":"Campbell","background":"#654321","purple":"#987654"}]}
        """, Schemes);
        Assert.AreEqual("#654321", settings.Options["theme"]!["background"]!.GetValue<string>());
        Assert.AreEqual("#123456", settings.Options["theme"]!["foreground"]!.GetValue<string>());
        Assert.AreEqual("#987654", settings.Options["theme"]!["magenta"]!.GetValue<string>());
        Assert.AreEqual("bar", settings.Options["cursorStyle"]!.GetValue<string>());
    }

    [TestMethod]
    public void EmptySettingsUsePowerShellAndCampbellDefaults()
    {
        var settings = WindowsTerminalSettings.Parse("{}", Schemes);
        Assert.AreEqual("powershell.exe", settings.CommandLine);
        Assert.AreEqual("#0c0c0c", settings.Options["theme"]!["background"]!.GetValue<string>());
        Assert.AreEqual("#881798", settings.Options["theme"]!["magenta"]!.GetValue<string>());
        Assert.AreEqual(16d, settings.Options["fontSize"]!.GetValue<double>());
    }
}
