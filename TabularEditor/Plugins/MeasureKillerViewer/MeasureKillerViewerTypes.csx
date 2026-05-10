using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

internal enum EffectiveUsage
{
    Keep,
    CascadeCandidate,
    RelationshipOnly,
    RelationshipIsland,
    DuplicateVisual,
    Unused,
    Review
}

internal enum LineageEdgeKind
{
    MeasureDependency,
    Visual,
    VisualFilter,
    PageFilter,
    ReportFilter,
    Relationship,
    SortBy,
    Hierarchy,
    FieldParameter,
    CalculationItem,
    RowLevelSecurity,
    Artifact,
    Unknown
}

internal class LineageObject
{
    public string Key;
    public string ObjectType;
    public string TableName;
    public string Name;
    public string DirectUsage;
    public EffectiveUsage EffectiveUsage;
    public string Reason;
    public string Recommendation;
    public string SearchText;
    public object SourceObject;
    public List<LineageEdge> UsedBy = new List<LineageEdge>();
    public Dictionary<string, string> RawProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

internal class LineageEdge
{
    public string FromKey;
    public string ToKey;
    public LineageEdgeKind Kind;
    public string Label;
}

internal enum ImpactKind
{
    Neutral,
    Used,
    Unused,
    Relationship,
    RelationshipOnly,
    RelationshipOnlyIsolated,
    MeasureOnlyDeadEnd,
    Model,
    Report
}

internal class MkNode
{
    public readonly string Text;
    public readonly ImpactKind Impact;
    public readonly string Tooltip;
    public readonly List<MkNode> Children = new List<MkNode>();
    public string SearchText;
    public string Path;
    public string ObjectType;
    public object SourceObject;
    public bool IsUnused { get { return Impact == ImpactKind.Unused; } }

    public MkNode(string text, bool isUnused, string tooltip)
        : this(text, isUnused ? ImpactKind.Unused : ImpactKind.Used, tooltip)
    {
    }

    public MkNode(string text, ImpactKind impact, string tooltip)
    {
        Text = text;
        Impact = impact;
        Tooltip = tooltip;
        SearchText = text + " " + tooltip;
        Path = text;
    }

    public MkNode Add(string text, bool isUnused, string tooltip)
    {
        var child = new MkNode(text, isUnused, tooltip);
        child.Path = Path + "/" + text;
        Children.Add(child);
        return child;
    }

    public MkNode Add(string text, ImpactKind impact, string tooltip)
    {
        var child = new MkNode(text, impact, tooltip);
        child.Path = Path + "/" + text;
        Children.Add(child);
        return child;
    }
}

internal class VisualBox
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
    public string Title;
    public string Page;
    public string UsedAs;
    public string VisualId;
    public bool Highlight;
}

internal static class Json
{
    public static Dictionary<string, object> ReadObject(string text)
    {
        return new Parser(text).ParseObject();
    }

    public static object Value(Dictionary<string, object> dict, string key)
    {
        object value;
        return dict != null && dict.TryGetValue(key, out value) ? value : null;
    }

    public static string Str(Dictionary<string, object> dict, string key)
    {
        var value = Value(dict, key);
        return value == null ? "" : Convert.ToString(value);
    }

    public static ArrayList Array(Dictionary<string, object> dict, string key)
    {
        return Value(dict, key) as ArrayList ?? new ArrayList();
    }

    private sealed class Parser
    {
        private readonly string text;
        private int index;

        public Parser(string text)
        {
            this.text = text ?? "";
        }

        public Dictionary<string, object> ParseObject()
        {
            SkipWhiteSpace();
            return ReadObject();
        }

        private object ReadValue()
        {
            SkipWhiteSpace();
            if (index >= text.Length) throw new FormatException("Unexpected end of JSON.");
            var ch = text[index];
            if (ch == '{') return ReadObject();
            if (ch == '[') return ReadArray();
            if (ch == '"') return ReadString();
            if (ch == 't') return ReadLiteral("true", true);
            if (ch == 'f') return ReadLiteral("false", false);
            if (ch == 'n') return ReadLiteral("null", null);
            return ReadNumber();
        }

        private Dictionary<string, object> ReadObject()
        {
            Expect('{');
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            SkipWhiteSpace();
            if (TryRead('}')) return result;

            while (true)
            {
                SkipWhiteSpace();
                var key = ReadString();
                SkipWhiteSpace();
                Expect(':');
                result[key] = ReadValue();
                SkipWhiteSpace();
                if (TryRead('}')) return result;
                Expect(',');
            }
        }

        private ArrayList ReadArray()
        {
            Expect('[');
            var result = new ArrayList();
            SkipWhiteSpace();
            if (TryRead(']')) return result;

            while (true)
            {
                result.Add(ReadValue());
                SkipWhiteSpace();
                if (TryRead(']')) return result;
                Expect(',');
            }
        }

        private string ReadString()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (index < text.Length)
            {
                var ch = text[index++];
                if (ch == '"') return builder.ToString();
                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (index >= text.Length) throw new FormatException("Invalid JSON string escape.");
                var escaped = text[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new FormatException("Invalid JSON unicode escape.");
                        builder.Append((char)Convert.ToInt32(text.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default:
                        throw new FormatException("Invalid JSON string escape.");
                }
            }
            throw new FormatException("Unterminated JSON string.");
        }

        private object ReadNumber()
        {
            var start = index;
            if (text[index] == '-') index++;
            while (index < text.Length && char.IsDigit(text[index])) index++;
            var isDecimal = false;
            if (index < text.Length && text[index] == '.')
            {
                isDecimal = true;
                index++;
                while (index < text.Length && char.IsDigit(text[index])) index++;
            }
            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                isDecimal = true;
                index++;
                if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                while (index < text.Length && char.IsDigit(text[index])) index++;
            }

            var value = text.Substring(start, index - start);
            if (isDecimal)
            {
                double d;
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
            }
            else
            {
                long l;
                if (long.TryParse(value, out l)) return l;
            }
            throw new FormatException("Invalid JSON number.");
        }

        private object ReadLiteral(string literal, object value)
        {
            if (index + literal.Length > text.Length || text.Substring(index, literal.Length) != literal)
                throw new FormatException("Invalid JSON literal.");
            index += literal.Length;
            return value;
        }

        private void Expect(char ch)
        {
            SkipWhiteSpace();
            if (index >= text.Length || text[index] != ch)
                throw new FormatException("Expected '" + ch + "'.");
            index++;
        }

        private bool TryRead(char ch)
        {
            SkipWhiteSpace();
            if (index >= text.Length || text[index] != ch) return false;
            index++;
            return true;
        }

        private void SkipWhiteSpace()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        }
    }
}
