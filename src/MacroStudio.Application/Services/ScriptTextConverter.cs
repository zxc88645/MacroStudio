using System.Globalization;
using System.Text;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Application.Services;

/// <summary>
/// Converts between the internal command list representation and a simple text script DSL.
/// </summary>
public static class ScriptTextConverter
{
    /// <summary>
    /// Converts the commands in a script to a textual representation.
    /// </summary>
    public static string ToText(Script script)
    {
        if (script == null) throw new ArgumentNullException(nameof(script));

        var sb = new StringBuilder();

        foreach (var command in script.Commands)
        {
            switch (command)
            {
                case MouseMoveCommand move:
                    sb.AppendLine($"move({move.Position.X}, {move.Position.Y})");
                    break;

                case MouseClickCommand click:
                    var buttonName = ToButtonName(click.Button);
                    var fn = click.Type switch
                    {
                        ClickType.Down => "mouse_down",
                        ClickType.Up => "mouse_release",
                        ClickType.Click => "mouse_click",
                        _ => "mouse_click"
                    };
                    sb.AppendLine($"{fn}('{buttonName}')");
                    break;

                case SleepCommand sleep:
                    var seconds = sleep.Duration.TotalSeconds;
                    sb.AppendLine($"sleep({seconds.ToString("0.###", CultureInfo.InvariantCulture)})");
                    break;

                case KeyboardCommand keyboard when !string.IsNullOrEmpty(keyboard.Text):
                    sb.AppendLine($"type_text('{EscapeSingleQuotes(keyboard.Text)}')");
                    break;

                case KeyboardCommand keyboard when keyboard.Keys.Count == 1:
                    var keyName = ToKeyCharName(keyboard.Keys[0]);
                    sb.AppendLine($"key_down('{keyName}')  # key command");
                    break;

                case KeyboardCommand keyboard:
                    var keyList = string.Join("+", keyboard.Keys.Select(k => k.ToString()));
                    sb.AppendLine($"# keys: {keyList}");
                    break;

                default:
                    sb.AppendLine($"# Unsupported command type: {command.DisplayName}");
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses text into a list of commands. Throws <see cref="FormatException"/> on invalid syntax.
    /// </summary>
    public static IReadOnlyList<Command> Parse(string? scriptText)
    {
        var commands = new List<Command>();

        if (string.IsNullOrWhiteSpace(scriptText))
            return commands;

        var lines = scriptText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        for (var i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var lineNumber = i + 1;

            // Strip comments
            var codePart = rawLine.Split('#', 2)[0].Trim();
            if (string.IsNullOrWhiteSpace(codePart))
                continue;

            try
            {
                if (codePart.StartsWith("mouse_down", StringComparison.OrdinalIgnoreCase))
                {
                    var button = ParseMouseButtonSingleArg(codePart, "mouse_down");
                    commands.Add(new MouseClickCommand(button, ClickType.Down));
                }
                else if (codePart.StartsWith("mouse_release", StringComparison.OrdinalIgnoreCase))
                {
                    var button = ParseMouseButtonSingleArg(codePart, "mouse_release");
                    commands.Add(new MouseClickCommand(button, ClickType.Up));
                }
                else if (codePart.StartsWith("mouse_click", StringComparison.OrdinalIgnoreCase))
                {
                    var button = ParseMouseButtonSingleArg(codePart, "mouse_click");
                    commands.Add(new MouseClickCommand(button, ClickType.Click));
                }
                else if (codePart.StartsWith("sleep", StringComparison.OrdinalIgnoreCase))
                {
                    var seconds = ParseSingleDoubleArg(codePart, "sleep");
                    commands.Add(new SleepCommand(TimeSpan.FromSeconds(seconds)));
                }
                else if (codePart.StartsWith("move", StringComparison.OrdinalIgnoreCase))
                {
                    var (x, y) = ParseTwoIntArgs(codePart, "move");
                    commands.Add(new MouseMoveCommand(new Point(x, y)));
                }
                else if (codePart.StartsWith("type_text", StringComparison.OrdinalIgnoreCase))
                {
                    var text = ParseSingleStringArg(codePart, "type_text");
                    commands.Add(new KeyboardCommand(text));
                }
                else if (codePart.StartsWith("key_down", StringComparison.OrdinalIgnoreCase) ||
                         codePart.StartsWith("key_release", StringComparison.OrdinalIgnoreCase))
                {
                    // For now, interpret key_down/key_release as a simple key press (down+up).
                    var keyChar = ParseSingleStringArg(codePart,
                        codePart.StartsWith("key_down", StringComparison.OrdinalIgnoreCase)
                            ? "key_down"
                            : "key_release");

                    var vk = ParseVirtualKeyFromChar(keyChar);
                    commands.Add(new KeyboardCommand(vk));
                }
                else
                {
                    throw new FormatException($"Unknown command: {codePart}");
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new FormatException($"Error on line {lineNumber}: {ex.Message}", ex);
            }
        }

        return commands;
    }

    private static string EscapeSingleQuotes(string text) =>
        text.Replace("'", "\\'");

    private static string ParseSingleStringArg(string code, string name)
    {
        var (inner, _) = ExtractInnerArgs(code, name);
        inner = inner.Trim();

        if (inner.Length < 2 || inner[0] != '\'' || inner[^1] != '\'')
            throw new FormatException($"Expected '{name}('value')' with single quotes.");

        var content = inner.Substring(1, inner.Length - 2);
        return content.Replace("\\'", "'");
    }

    private static double ParseSingleDoubleArg(string code, string name)
    {
        var (inner, _) = ExtractInnerArgs(code, name);
        inner = inner.Trim();

        if (!double.TryParse(inner, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Invalid numeric value for {name}: {inner}");

        if (value < 0)
            throw new FormatException($"{name} value cannot be negative.");

        return value;
    }

    private static (int x, int y) ParseTwoIntArgs(string code, string name)
    {
        var (inner, _) = ExtractInnerArgs(code, name);
        var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new FormatException($"Expected two arguments for {name}(x, y).");

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            throw new FormatException($"Invalid integer arguments for {name}(x, y).");
        }

        if (x < 0 || y < 0)
            throw new FormatException($"{name} coordinates must be non-negative.");

        return (x, y);
    }

    private static MouseButton ParseMouseButtonSingleArg(string code, string name)
    {
        var arg = ParseSingleStringArg(code, name).ToLowerInvariant();

        return arg switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => throw new FormatException($"Unsupported mouse button '{arg}'. Expected 'left', 'right', or 'middle'.")
        };
    }

    private static VirtualKey ParseVirtualKeyFromChar(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new FormatException("Key cannot be empty.");

        if (key.Length == 1)
        {
            var c = key[0];

            if (char.IsLetter(c))
            {
                var upper = char.ToUpperInvariant(c);
                var name = $"VK_{upper}";
                if (Enum.TryParse<VirtualKey>(name, out var vk))
                    return vk;
            }

            if (char.IsDigit(c))
            {
                var name = $"VK_{c}";
                if (Enum.TryParse<VirtualKey>(name, out var vk))
                    return vk;
            }
        }

        // Fallback: try raw enum name
        if (Enum.TryParse<VirtualKey>(key, true, out var parsed))
            return parsed;

        throw new FormatException($"Unsupported key '{key}'.");
    }

    private static (string inner, int startIndex) ExtractInnerArgs(string code, string name)
    {
        var openIdx = code.IndexOf('(');
        var closeIdx = code.LastIndexOf(')');

        if (openIdx < 0 || closeIdx <= openIdx)
            throw new FormatException($"Expected syntax {name}(...).");

        var fnName = code[..openIdx].Trim();
        if (!fnName.Equals(name, StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Expected function name '{name}', got '{fnName}'.");

        var inner = code.Substring(openIdx + 1, closeIdx - openIdx - 1);
        return (inner, openIdx + 1);
    }

    private static string ToButtonName(MouseButton button) =>
        button switch
        {
            MouseButton.Left => "left",
            MouseButton.Right => "right",
            MouseButton.Middle => "middle",
            _ => button.ToString().ToLowerInvariant()
        };

    private static string ToKeyCharName(VirtualKey vk)
    {
        var name = vk.ToString();
        if (name.StartsWith("VK_", StringComparison.Ordinal))
        {
            var shortName = name.Substring(3);
            if (shortName.Length == 1)
                return shortName.ToLowerInvariant();
        }

        return name;
    }
}

