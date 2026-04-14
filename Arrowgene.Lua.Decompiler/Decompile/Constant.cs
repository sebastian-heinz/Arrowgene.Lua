using System;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Constant. A wrapper over a parsed <see cref="LObject"/>
/// that classifies the value (nil/boolean/number/string), exposes type queries
/// used by the expression layer, and recognizes Lua identifiers.
/// </summary>
public sealed class Constant
{
    private enum ConstantType
    {
        NIL,
        BOOLEAN,
        NUMBER,
        STRING,
    }

    private readonly ConstantType type;
    private readonly bool @bool;
    private readonly LNumber number;
    private readonly string @string;

    public Constant(int constant)
    {
        type = ConstantType.NUMBER;
        @bool = false;
        number = LNumber.MakeInteger(constant);
        @string = null;
    }

    public Constant(double x)
    {
        type = ConstantType.NUMBER;
        @bool = false;
        number = LNumber.MakeDouble(x);
        @string = null;
    }

    public Constant(LObject constant)
    {
        if (constant is LNil)
        {
            type = ConstantType.NIL;
            @bool = false;
            number = null;
            @string = null;
        }
        else if (constant is LBoolean lb)
        {
            type = ConstantType.BOOLEAN;
            @bool = lb == LBoolean.LTRUE;
            number = null;
            @string = null;
        }
        else if (constant is LNumber ln)
        {
            type = ConstantType.NUMBER;
            @bool = false;
            number = ln;
            @string = null;
        }
        else if (constant is LString ls)
        {
            type = ConstantType.STRING;
            @bool = false;
            number = null;
            @string = ls.Deref();
        }
        else
        {
            throw new ArgumentException("Illegal constant type: " + constant);
        }
    }

    public void Print(Decompiler decompiler, Output @out, bool braced)
    {
        switch (type)
        {
            case ConstantType.NIL:
                @out.Print("nil");
                break;
            case ConstantType.BOOLEAN:
                @out.Print(@bool ? "true" : "false");
                break;
            case ConstantType.NUMBER:
                @out.Print(number.ToPrintString(0));
                break;
            case ConstantType.STRING:
            {
                int newlines = 0;
                int unprintable = 0;
                bool rawstring = decompiler.GetConfiguration().rawstring;
                for (int i = 0; i < @string.Length; i++)
                {
                    char c = @string[i];
                    if (c == '\n')
                    {
                        newlines++;
                    }
                    else if ((c <= 31 && c != '\t') || c >= 127)
                    {
                        unprintable++;
                    }
                }
                bool longString = (newlines > 1 || (newlines == 1 && @string.IndexOf('\n') != @string.Length - 1));
                longString = longString && unprintable == 0;
                longString = longString && !@string.Contains("[[");
                if (decompiler.function.header.version.usenestinglongstrings.Get())
                {
                    longString = longString && !@string.Contains("]]") && !@string.EndsWith("]");
                }
                if (longString)
                {
                    int pipe = 0;
                    string pipeString = "]]";
                    string startPipeString = "]";
                    while (@string.EndsWith(startPipeString) || @string.IndexOf(pipeString) >= 0)
                    {
                        pipe++;
                        pipeString = "]";
                        int p = pipe;
                        while (p-- > 0) pipeString += "=";
                        startPipeString = pipeString;
                        pipeString += "]";
                    }
                    if (braced) @out.Print("(");
                    @out.Print("[");
                    while (pipe-- > 0) @out.Print("=");
                    @out.Print("[");
                    int indent = @out.GetIndentationLevel();
                    @out.SetIndentationLevel(0);
                    @out.PrintLn();
                    @out.Print(@string);
                    @out.Print(pipeString);
                    if (braced) @out.Print(")");
                    @out.SetIndentationLevel(indent);
                }
                else
                {
                    @out.Print("\"");
                    for (int i = 0; i < @string.Length; i++)
                    {
                        char c = @string[i];
                        if (c <= 31 || c >= 127)
                        {
                            if (c == 7) @out.Print("\\a");
                            else if (c == 8) @out.Print("\\b");
                            else if (c == 12) @out.Print("\\f");
                            else if (c == 10) @out.Print("\\n");
                            else if (c == 13) @out.Print("\\r");
                            else if (c == 9) @out.Print("\\t");
                            else if (c == 11) @out.Print("\\v");
                            else if (!rawstring || c <= 127)
                            {
                                string dec = ((int)c).ToString();
                                int len = dec.Length;
                                @out.Print("\\");
                                while (len++ < 3) @out.Print("0");
                                @out.Print(dec);
                            }
                            else
                            {
                                @out.PrintByte((byte)c);
                            }
                        }
                        else if (c == 34)
                        {
                            @out.Print("\\\"");
                        }
                        else if (c == 92)
                        {
                            @out.Print("\\\\");
                        }
                        else
                        {
                            @out.Print(c.ToString());
                        }
                    }
                    @out.Print("\"");
                }
                break;
            }
            default:
                throw new InvalidOperationException();
        }
    }

    public bool IsNil()     => type == ConstantType.NIL;
    public bool IsBoolean() => type == ConstantType.BOOLEAN;
    public bool IsNumber()  => type == ConstantType.NUMBER;

    public bool IsInteger()
    {
        return number.Value() == Math.Round(number.Value());
    }

    public bool IsNegative()
    {
        // Tricky to catch -0.0 here.
        return number.Value().ToString(System.Globalization.CultureInfo.InvariantCulture).StartsWith("-");
    }

    public int AsInteger()
    {
        if (!IsInteger())
        {
            throw new InvalidOperationException();
        }
        return (int)number.Value();
    }

    public bool IsString() => type == ConstantType.STRING;

    public bool IsIdentifierPermissive(Version version)
    {
        if (!IsString() || version.IsReserved(@string))
        {
            return false;
        }
        if (@string.Length == 0)
        {
            return false;
        }
        char start = @string[0];
        if (char.IsDigit(start) && start != ' ' && !char.IsLetter(start))
        {
            return false;
        }
        return true;
    }

    public bool IsIdentifier(Version version)
    {
        if (!IsIdentifierPermissive(version))
        {
            return false;
        }
        char start = @string[0];
        if (start != '_' && !char.IsLetter(start))
        {
            return false;
        }
        for (int i = 1; i < @string.Length; i++)
        {
            char next = @string[i];
            if (char.IsLetter(next)) continue;
            if (char.IsDigit(next)) continue;
            if (next == '_') continue;
            return false;
        }
        return true;
    }

    public string AsName()
    {
        if (type != ConstantType.STRING)
        {
            throw new InvalidOperationException();
        }
        return @string;
    }
}
