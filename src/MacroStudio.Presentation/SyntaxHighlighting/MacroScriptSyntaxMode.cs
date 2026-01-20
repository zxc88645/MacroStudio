using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.IO;
using System.Windows.Media;
using System.Xml;

namespace MacroStudio.Presentation.SyntaxHighlighting;

/// <summary>
/// Provides syntax highlighting for Macro Studio script DSL.
/// </summary>
public static class MacroScriptSyntaxMode
{
    private static IHighlightingDefinition? _highlightingDefinition;

    /// <summary>
    /// Gets the highlighting definition for Macro Studio scripts.
    /// </summary>
    public static IHighlightingDefinition GetHighlightingDefinition()
    {
        if (_highlightingDefinition != null)
            return _highlightingDefinition;

        using var reader = new StringReader(GetXshdContent());
        using var xmlReader = new XmlTextReader(reader);
        _highlightingDefinition = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
        return _highlightingDefinition;
    }

    private static string GetXshdContent()
    {
        return @"<?xml version=""1.0""?>
<SyntaxDefinition name=""MacroScript"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <Color name=""Function"" foreground=""#3b82f6"" />
  <Color name=""String"" foreground=""#22c55e"" />
  <Color name=""Number"" foreground=""#f59e0b"" />
  <Color name=""Comment"" foreground=""#9ca3af"" />
  <Color name=""Keyword"" foreground=""#a855f7"" />
  
  <RuleSet>
    <!-- Comments - use Span to avoid zero-width match issues -->
    <Span color=""Comment"" begin=""#"" end=""\n"" />
    
    <!-- Strings (single quotes) - must come after comments to avoid conflicts -->
    <Span color=""String"" begin=""'"" end=""'"" />
    
    <!-- Functions -->
    <Rule color=""Function"">
      \b(mouse_down|mouse_release|mouse_click|sleep|move|type_text|key_down|key_release)\b
    </Rule>
    
    <!-- Numbers -->
    <Rule color=""Number"">
      \b\d+\.?\d*\b
    </Rule>
    
    <!-- Keywords (button names, etc.) -->
    <Rule color=""Keyword"">
      \b(left|right|middle)\b
    </Rule>
  </RuleSet>
</SyntaxDefinition>";
    }
}
