using System.IO;
using System.Text;

namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.Tokenizer. A small line-aware lexer for the
/// textual disassembly format: hands the assembler successive whitespace-
/// delimited tokens, with two tweaks beyond a plain split. Lua-style
/// double-quoted strings (optionally prefixed with <c>L</c> for the long
/// form) are kept as a single token honouring <c>\</c> escapes, and
/// comments starting with <c>;</c> run through end of line. Tracks the
/// 1-based line and column where each token began so error reporting can
/// point back into the source.
/// </summary>
public sealed class Tokenizer
{
    private readonly StringBuilder b;
    private readonly Stream _in;
    private int line;
    private int pos;
    private int tokenline;
    private int tokenpos;
    private char lineending;

    public Tokenizer(Stream input)
    {
        this._in = input;
        this.line = 1;
        this.pos = 0;
        this.tokenline = 1;
        this.tokenpos = 0;
        this.lineending = '\0';
        b = new StringBuilder();
    }

    public string Next()
    {
        b.Length = 0;

        bool inToken = false;
        bool inString = false;
        bool inComment = false;
        bool isLPrefix = false;
        bool inEscape = false;

        for (;;)
        {
            if (!inToken)
            {
                tokenline = line;
                tokenpos = pos;
            }
            int code = _in.ReadByte();
            if (code == -1) break;
            pos++;
            char c = (char)code;
            char lastlineending = lineending;
            lineending = '\0';
            if (lastlineending == '\r' && c == '\n')
            {
                // skip
            }
            else if (inString)
            {
                if (c == '\\' && !inEscape)
                {
                    inEscape = true;
                    b.Append(c);
                }
                else if (c == '"' && !inEscape)
                {
                    b.Append(c);
                    break;
                }
                else
                {
                    inEscape = false;
                    b.Append(c);
                }
            }
            else if (inComment)
            {
                if (c == '\n' || c == '\r')
                {
                    line++;
                    pos = 0;
                    lineending = c;
                    inComment = false;
                    if (inToken)
                    {
                        break;
                    }
                }
            }
            else if (c == ';')
            {
                inComment = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (c == '\n' || c == '\r')
                {
                    line++;
                    pos = 0;
                    lineending = c;
                }
                if (inToken)
                {
                    break;
                }
            }
            else
            {
                if ((!inToken || isLPrefix) && c == '"')
                {
                    inString = true;
                }
                else if (!inToken && c == 'L')
                {
                    isLPrefix = true;
                }
                else
                {
                    isLPrefix = false;
                }
                inToken = true;
                b.Append(c);
            }
        }

        if (b.Length == 0)
        {
            return null;
        }
        return b.ToString();
    }

    public int Line() => tokenline;

    public int Pos() => tokenpos;
}
