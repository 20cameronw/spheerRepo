using System;

namespace Frontier
{
    /// <summary>Cheap structural checks on JSON text, so a truncated file is recognised even by a lenient parser.</summary>
    public static class JsonShape
    {
        /// <summary>True when the text is one complete JSON object: braces and quotes balance and nothing but whitespace follows the closing brace.</summary>
        public static bool IsCompleteObject(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int i = 0, n = text.Length;
            while (i < n && (text[i] == ' ' || text[i] == '\t' || text[i] == '\r' || text[i] == '\n' || text[i] == '\uFEFF')) i++;
            if (i >= n || text[i] != '{') return false;
            int depth = 0;
            bool inString = false;
            for (; i < n; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth < 0) return false;
                    if (depth == 0)
                    {
                        for (int k = i + 1; k < n; k++)
                            if (text[k] != ' ' && text[k] != '\t' && text[k] != '\r' && text[k] != '\n') return false;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
