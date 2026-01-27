using System.Globalization;
using System.Text;
using MacroNex.Domain.Entities;
using MacroNex.Domain.ValueObjects;

namespace MacroNex.Application.Services;

/// <summary>
/// Converts recorded commands to Lua SourceText.
/// </summary>
public static class ScriptTextConverter
{
    /// <summary>
    /// Converts a list of commands to Lua SourceText.
    /// </summary>
    public static string CommandsToText(IEnumerable<Command> commands)
    {
        if (commands == null) throw new ArgumentNullException(nameof(commands));

        var sb = new StringBuilder();

        foreach (var command in commands)
        {
            // Preserve timing: Command.Delay is the time gap since the previous command.
            // In Lua/text form we represent that gap explicitly via msleep(...) so
            // recorded scripts keep realistic pacing when executed by LuaScriptRunner.
            AppendDelayIfAny(sb, command.Delay);

            AppendCommand(sb, command);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts the commands in a script to a textual representation.
    /// </summary>
    public static string ToText(Script script)
    {
        if (script == null) throw new ArgumentNullException(nameof(script));
        return CommandsToText(script.Commands);
    }

    private static void AppendCommand(StringBuilder sb, Command command)
    {
        switch (command)
        {
            // All move commands use unified functions - actual behavior depends on InputMode setting
            case MouseMoveCommand move:
                sb.AppendLine($"move({move.Position.X}, {move.Position.Y})");
                break;

            case MouseMoveRelativeCommand moveRel:
                sb.AppendLine($"move_rel({moveRel.DeltaX}, {moveRel.DeltaY})");
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
                AppendMsleepIfAny(sb, sleep.Duration);
                break;

            case KeyboardCommand keyboard when !string.IsNullOrEmpty(keyboard.Text):
                sb.AppendLine($"type_text('{EscapeSingleQuotes(keyboard.Text)}')");
                break;

            case KeyPressCommand kp:
                var keyName = ToKeyCharName(kp.Key);
                sb.AppendLine($"{(kp.IsDown ? "key_down" : "key_release")}('{keyName}')");
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

    private static void AppendDelayIfAny(StringBuilder sb, TimeSpan delay)
    {
        AppendMsleepIfAny(sb, delay);
    }

    private static void AppendMsleepIfAny(StringBuilder sb, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return;

        // As requested: always emit msleep with integer milliseconds.
        // Round to nearest millisecond to avoid systematic bias.
        var ms = (long)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero);
        if (ms <= 0)
            return;

        sb.AppendLine($"msleep({ms.ToString(CultureInfo.InvariantCulture)})");
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
                else if (codePart.StartsWith("msleep", StringComparison.OrdinalIgnoreCase))
                {
                    var ms = ParseSingleDoubleArg(codePart, "msleep");
                    commands.Add(new SleepCommand(TimeSpan.FromMilliseconds(ms)));
                }
                else if (codePart.StartsWith("move_rel", StringComparison.OrdinalIgnoreCase))
                {
                    var (dx, dy) = ParseTwoIntArgs(codePart, "move_rel");
                    commands.Add(new MouseMoveRelativeCommand(dx, dy));
                }
                else if (codePart.StartsWith("move", StringComparison.OrdinalIgnoreCase))
                {
                    var (x, y) = ParseTwoIntArgs(codePart, "move");
                    commands.Add(new MouseMoveCommand(new Point(x, y)));
                }
                else if (codePart.StartsWith("type_text", StringComparison.OrdinalIgnoreCase))
                {
                    var text = ParseSingleStringArg(codePart, "type_text");
                    if (string.IsNullOrEmpty(text))
                    {
                        throw new FormatException("type_text cannot be empty. Provide text to type or remove the command.");
                    }
                    commands.Add(new KeyboardCommand(text));
                }
                else if (codePart.StartsWith("key_down", StringComparison.OrdinalIgnoreCase) ||
                         codePart.StartsWith("key_release", StringComparison.OrdinalIgnoreCase))
                {
                    var isDown = codePart.StartsWith("key_down", StringComparison.OrdinalIgnoreCase);
                    var keyChar = ParseSingleStringArg(codePart, isDown ? "key_down" : "key_release");
                    var vk = ParseVirtualKeyFromChar(keyChar);
                    commands.Add(new KeyPressCommand(vk, isDown));
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

        // Only validate non-negative for absolute positions (move)
        // Relative movements (move_rel) can have negative values
        if (!name.Contains("rel", StringComparison.OrdinalIgnoreCase))
        {
            if (x < 0 || y < 0)
                throw new FormatException($"{name} coordinates must be non-negative.");
        }

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

