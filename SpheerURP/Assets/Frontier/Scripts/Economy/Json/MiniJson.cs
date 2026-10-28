using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Frontier.Economy
{
    /// <summary>
    /// A small strict JSON reader for the economy exports (docs/economy/data). It produces <c>Dictionary&lt;string, object&gt;</c>,
    /// <c>List&lt;object&gt;</c>, <c>string</c>, <c>double</c>, <c>bool</c> and <c>null</c>. Unity's JsonUtility cannot read enum names, arrays at
    /// the root or nulls, and the Newtonsoft package is not a dependency of the project, so the importer and the plain .NET tests share this.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            var reader = new Reader(text);
            reader.SkipSpace();
            object value = reader.Value();
            reader.SkipSpace();
            if (!reader.AtEnd) throw reader.Error("unexpected text after the JSON value");
            return value;
        }

        sealed class Reader
        {
            readonly string s;
            int i;

            public Reader(string text) { s = text; i = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0; }

            public bool AtEnd { get { return i >= s.Length; } }

            public FormatException Error(string message) { return new FormatException("JSON: " + message + " at offset " + i); }

            public void SkipSpace()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
            }

            public object Value()
            {
                if (AtEnd) throw Error("unexpected end");
                char c = s[i];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (c == '-' || (c >= '0' && c <= '9')) return Num();
                if (Literal("true")) return true;
                if (Literal("false")) return false;
                if (Literal("null")) return null;
                throw Error("unexpected character '" + c + "'");
            }

            bool Literal(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
                i += word.Length;
                return true;
            }

            Dictionary<string, object> Obj()
            {
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                i++;
                SkipSpace();
                if (!AtEnd && s[i] == '}') { i++; return dict; }
                while (true)
                {
                    SkipSpace();
                    if (AtEnd || s[i] != '"') throw Error("expected a property name");
                    string key = Str();
                    SkipSpace();
                    if (AtEnd || s[i] != ':') throw Error("expected ':'");
                    i++;
                    SkipSpace();
                    if (dict.ContainsKey(key)) throw Error("duplicate property '" + key + "'");
                    dict.Add(key, Value());
                    SkipSpace();
                    if (AtEnd) throw Error("unterminated object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return dict; }
                    throw Error("expected ',' or '}'");
                }
            }

            List<object> Arr()
            {
                var list = new List<object>();
                i++;
                SkipSpace();
                if (!AtEnd && s[i] == ']') { i++; return list; }
                while (true)
                {
                    SkipSpace();
                    list.Add(Value());
                    SkipSpace();
                    if (AtEnd) throw Error("unterminated array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw Error("expected ',' or ']'");
                }
            }

            string Str()
            {
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw Error("unescaped control character");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("unterminated escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Error("short unicode escape");
                            int code;
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) throw Error("bad unicode escape");
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw Error("unknown escape '\\" + e + "'");
                    }
                }
            }

            double Num()
            {
                int start = i;
                if (s[i] == '-') i++;
                if (AtEnd || s[i] < '0' || s[i] > '9') throw Error("expected digit");
                if (s[i] == '0') i++;
                else while (!AtEnd && s[i] >= '0' && s[i] <= '9') i++;
                if (!AtEnd && s[i] == '.')
                {
                    i++;
                    int digits = i;
                    while (!AtEnd && s[i] >= '0' && s[i] <= '9') i++;
                    if (digits == i) throw Error("expected fraction digits");
                }
                if (!AtEnd && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++;
                    if (!AtEnd && (s[i] == '+' || s[i] == '-')) i++;
                    int digits = i;
                    while (!AtEnd && s[i] >= '0' && s[i] <= '9') i++;
                    if (digits == i) throw Error("expected exponent digits");
                }
                double d;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ||
                    double.IsInfinity(d) || double.IsNaN(d)) throw Error("bad number");
                return d;
            }
        }
    }
}
