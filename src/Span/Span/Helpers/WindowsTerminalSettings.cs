using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Span.Helpers;

public sealed record WindowsTerminalSettings(string CommandLine, JsonObject Options, string? BackgroundImage,
    double BackgroundOpacity, string BackgroundSize, string Padding)
{
    private static readonly JsonDocumentOptions JsonOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static WindowsTerminalSettings Load()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[] {
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(local, "Microsoft", "Windows Terminal", "settings.json"),
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json") };
        string schemes = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal", "windows-terminal-schemes.json"));
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try { return Parse(File.ReadAllText(path), schemes); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            { DebugLogger.Log("[Terminal settings] " + ex.Message); }
        }
        return Parse("{}", schemes);
    }

    public static WindowsTerminalSettings Parse(string json, string schemesJson)
    {
        var root = JsonNode.Parse(json, documentOptions: JsonOptions)!.AsObject();
        var profiles = root["profiles"] as JsonObject;
        var list = profiles?["list"] as JsonArray ?? root["profiles"] as JsonArray ?? new JsonArray();
        string? defaultId = root["defaultProfile"]?.GetValue<string>();
        bool IsPowerShell(JsonNode? p) => p != null && (Text(p["name"]).Contains("PowerShell", StringComparison.OrdinalIgnoreCase)
            || Text(p["commandline"]).Contains("powershell", StringComparison.OrdinalIgnoreCase)
            || Text(p["commandline"]).Contains("pwsh", StringComparison.OrdinalIgnoreCase)
            || Text(p["source"]) == "Windows.Terminal.PowershellCore");
        var selected = list.FirstOrDefault(p => (Text(p?["guid"]) == defaultId || Text(p?["name"]) == defaultId) && IsPowerShell(p))
            ?? list.FirstOrDefault(p => IsPowerShell(p) && p?["hidden"]?.GetValue<bool>() != true);
        var settings = Merge(profiles?["defaults"] as JsonObject, selected as JsonObject);
        string command = Text(settings["commandline"]);
        if (string.IsNullOrWhiteSpace(command)) command = Text(selected?["source"]) == "Windows.Terminal.PowershellCore" ? "pwsh.exe" : "powershell.exe";
        command = Environment.ExpandEnvironmentVariables(command);
        var font = settings["font"] as JsonObject ?? new JsonObject();
        var schemeName = settings["colorScheme"] is JsonObject variants ? Text(variants["dark"]) : Text(settings["colorScheme"]);
        if (string.IsNullOrEmpty(schemeName)) schemeName = "Campbell";
        var builtins = JsonNode.Parse(schemesJson, documentOptions: JsonOptions)!.AsArray();
        var schemes = root["schemes"] as JsonArray ?? new JsonArray();
        var scheme = schemes.LastOrDefault(s => Text(s?["name"]) == schemeName)
            ?? builtins.FirstOrDefault(s => Text(s?["name"]) == schemeName)
            ?? builtins.FirstOrDefault(s => Text(s?["name"]) == "Campbell");
        var theme = new JsonObject();
        foreach (string key in new[] { "background", "foreground", "black", "red", "green", "yellow", "blue", "purple", "cyan", "white", "brightBlack", "brightRed", "brightGreen", "brightYellow", "brightBlue", "brightPurple", "brightCyan", "brightWhite", "cursorColor", "selectionBackground" })
        {
            var value = settings[key] ?? scheme?[key];
            if (value == null) continue;
            string target = key switch { "purple" => "magenta", "brightPurple" => "brightMagenta", "cursorColor" => "cursor", _ => key };
            theme[target] = value.DeepClone();
        }
        string shape = Text(settings["cursorShape"]);
        var options = new JsonObject {
            ["fontFamily"] = Text(font["face"] ?? settings["fontFace"], "Cascadia Mono") + ", monospace",
            // Terminal font.size is in points; WebView/xterm uses CSS pixels (96/72).
            ["fontSize"] = Number(font["size"] ?? settings["fontSize"], 12) * 96 / 72,
            ["fontWeight"] = font["weight"]?.DeepClone() ?? JsonValue.Create("normal"),
            ["cursorStyle"] = shape switch { "bar" => "bar", "underscore" or "doubleUnderscore" => "underline", _ => "block" },
            ["cursorBlink"] = true, ["scrollback"] = (int)Number(settings["historySize"], 9001), ["theme"] = theme
        };
        string? image = settings["backgroundImage"]?.GetValue<string>();
        if (image != null) image = Environment.ExpandEnvironmentVariables(image);
        string stretch = Text(settings["backgroundImageStretchMode"]);
        return new(command, options, image, Math.Clamp(Number(settings["backgroundImageOpacity"], 1), 0, 1),
            stretch switch { "fill" => "100% 100%", "uniform" => "contain", "none" => "auto", _ => "cover" }, Text(settings["padding"], "8,8,8,8"));
    }

    private static string Text(JsonNode? node, string fallback = "") => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : fallback;
    private static double Number(JsonNode? node, double fallback) => node is JsonValue value && value.TryGetValue<double>(out var number) ? number : fallback;
    private static JsonObject Merge(JsonObject? defaults, JsonObject? profile)
    {
        var result = defaults?.DeepClone().AsObject() ?? new JsonObject();
        if (profile != null) foreach (var pair in profile)
            result[pair.Key] = pair.Value is JsonObject obj && result[pair.Key] is JsonObject parent ? Merge(parent, obj) : pair.Value?.DeepClone();
        return result;
    }
}
