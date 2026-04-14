using System.Globalization;
using System.Text;

namespace Arrowgene.Lua.Decompiler.Util;

public static class StringUtils
{
    public static string ToPrintString(string s)
    {
        return ToPrintString(s, -1);
    }

    public static string ToPrintString(string s, int limit)
    {
        if (s == null) return "null";
        if (limit < 0) limit = s.Length;
        if (limit > s.Length) limit = s.Length;
        StringBuilder b = new StringBuilder();
        b.Append('"');
        for (int i = 0; i < limit; i++)
        {
            char c = s[i];
            int ci = c;
            if (c == '"')
            {
                b.Append("\\\"");
            }
            else if (c == '\\')
            {
                b.Append("\\\\");
            }
            else if (ci >= 32 && ci <= 126)
            {
                b.Append(c);
            }
            else if (c == '\n')
            {
                b.Append("\\n");
            }
            else if (c == '\t')
            {
                b.Append("\\t");
            }
            else if (c == '\r')
            {
                b.Append("\\r");
            }
            else if (c == '\b')
            {
                b.Append("\\b");
            }
            else if (c == '\f')
            {
                b.Append("\\f");
            }
            else if (ci == 11)
            {
                b.Append("\\v");
            }
            else if (ci == 7)
            {
                b.Append("\\a");
            }
            else
            {
                b.Append("\\x").Append(ci.ToString("x2", CultureInfo.InvariantCulture));
            }
        }
        b.Append('"');
        return b.ToString();
    }

    public static string FromPrintString(string s)
    {
        if (s.Equals("null")) return null;
        if (s[0] != '"') throw new System.InvalidOperationException("Bad string " + s);
        if (s[s.Length - 1] != '"') throw new System.InvalidOperationException("Bad string " + s);
        StringBuilder b = new StringBuilder();
        for (int i = 1; i < s.Length - 1; /* nothing */)
        {
            char c = s[i++];
            if (c == '\\')
            {
                if (i < s.Length - 1)
                {
                    c = s[i++];
                    if (c == '"') b.Append('"');
                    else if (c == '\\') b.Append('\\');
                    else if (c == 'n') b.Append('\n');
                    else if (c == 't') b.Append('\t');
                    else if (c == 'r') b.Append('\r');
                    else if (c == 'b') b.Append('\b');
                    else if (c == 'f') b.Append('\f');
                    else if (c == 'v') b.Append((char)11);
                    else if (c == 'a') b.Append((char)7);
                    else if (c == 'x')
                    {
                        if (i + 1 < s.Length - 1)
                        {
                            string digits = s.Substring(i, 2);
                            i += 2;
                            b.Append((char)int.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                b.Append(c);
            }
        }
        return b.ToString();
    }
}
