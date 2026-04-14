using System;
using System.Globalization;
using Arrowgene.Lua.Decompiler.Decompile;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LNumber plus its four concrete subclasses.</summary>
public abstract class LNumber : LObject
{
    public static LNumber MakeInteger(int number) => new LIntNumber(number);

    public static LNumber MakeDouble(double x) => new LDoubleNumber(x, LNumberType.NumberMode.MODE_FLOAT);

    public override abstract string ToPrintString(int flags);

    public abstract double Value();

    public abstract bool IntegralType();

    public abstract long Bits();
}

internal sealed class LFloatNumber : LNumber
{
    public const int NAN_SHIFT_OFFSET = 52 - 23;

    public readonly float number;
    public readonly LNumberType.NumberMode mode;

    public LFloatNumber(float number, LNumberType.NumberMode mode)
    {
        this.number = number;
        this.mode = mode;
    }

    public override string ToPrintString(int flags)
    {
        if (float.IsInfinity(number))
        {
            return number > 0.0 ? "1e9999" : "-1e9999";
        }
        if (float.IsNaN(number))
        {
            if (PrintFlag.Test(flags, PrintFlag.DISASSEMBLER))
            {
                int bits = BitConverter.SingleToInt32Bits(number);
                int canonical = BitConverter.SingleToInt32Bits(float.NaN);
                if (bits == canonical)
                {
                    return "NaN";
                }
                string sign = "+";
                if (bits < 0)
                {
                    bits ^= unchecked((int)0x80000000);
                    sign = "-";
                }
                long lbits = bits ^ canonical;
                // shift by difference in number of bits between double-precision and single-precision
                return "NaN" + sign + (lbits << NAN_SHIFT_OFFSET).ToString("x", CultureInfo.InvariantCulture);
            }
            return "(0/0)";
        }

        if (mode == LNumberType.NumberMode.MODE_NUMBER &&
            number >= int.MinValue &&
            number <= int.MaxValue &&
            number == (float)Math.Round(number))
        {
            if (BitConverter.SingleToInt32Bits(number) == BitConverter.SingleToInt32Bits(-0.0f))
            {
                return "-0";
            }
            return ((int)number).ToString(CultureInfo.InvariantCulture);
        }
        return FormatFiniteFloat(number);
    }

    public override bool Equals(object o)
    {
        if (o is LFloatNumber lf)
        {
            return BitConverter.SingleToInt32Bits(number) == BitConverter.SingleToInt32Bits(lf.number);
        }
        if (o is LNumber ln)
        {
            return Value() == ln.Value();
        }
        return false;
    }

    public override int GetHashCode() => BitConverter.SingleToInt32Bits(number);

    public override double Value() => number;
    public override bool IntegralType() => false;
    public override long Bits() => BitConverter.SingleToInt32Bits(number);

    private static string FormatFiniteFloat(float number)
    {
        if (BitConverter.SingleToInt32Bits(number) == BitConverter.SingleToInt32Bits(-0.0f))
        {
            return "-0.0";
        }
        string text = number.ToString("R", CultureInfo.InvariantCulture);
        return LNumberPrint.EnsureDecimalPoint(text);
    }
}

internal sealed class LDoubleNumber : LNumber
{
    public readonly double number;
    public readonly LNumberType.NumberMode mode;

    public LDoubleNumber(double number, LNumberType.NumberMode mode)
    {
        this.number = number;
        this.mode = mode;
    }

    public override string ToPrintString(int flags)
    {
        if (double.IsInfinity(number))
        {
            return number > 0.0 ? "1e9999" : "-1e9999";
        }
        if (double.IsNaN(number))
        {
            if (PrintFlag.Test(flags, PrintFlag.DISASSEMBLER))
            {
                long bits = BitConverter.DoubleToInt64Bits(number);
                long canonical = BitConverter.DoubleToInt64Bits(double.NaN);
                if (bits == canonical)
                {
                    return "NaN";
                }
                string sign = "+";
                if (bits < 0)
                {
                    bits ^= unchecked((long)0x8000000000000000L);
                    sign = "-";
                }
                return "NaN" + sign + (bits ^ canonical).ToString("x", CultureInfo.InvariantCulture);
            }
            return "(0/0)";
        }

        if (mode == LNumberType.NumberMode.MODE_NUMBER &&
            number >= long.MinValue &&
            number <= long.MaxValue &&
            number == Math.Round(number))
        {
            if (BitConverter.DoubleToInt64Bits(number) == BitConverter.DoubleToInt64Bits(-0.0))
            {
                return "-0";
            }
            return ((long)number).ToString(CultureInfo.InvariantCulture);
        }
        return FormatFiniteDouble(number);
    }

    public override bool Equals(object o)
    {
        if (o is LDoubleNumber ld)
        {
            return BitConverter.DoubleToInt64Bits(number) == BitConverter.DoubleToInt64Bits(ld.number);
        }
        if (o is LNumber ln)
        {
            return Value() == ln.Value();
        }
        return false;
    }

    public override int GetHashCode() => BitConverter.DoubleToInt64Bits(number).GetHashCode();

    public override double Value() => number;
    public override bool IntegralType() => false;
    public override long Bits() => BitConverter.DoubleToInt64Bits(number);

    private static string FormatFiniteDouble(double number)
    {
        if (BitConverter.DoubleToInt64Bits(number) == BitConverter.DoubleToInt64Bits(-0.0))
        {
            return "-0.0";
        }
        string text = number.ToString("R", CultureInfo.InvariantCulture);
        return LNumberPrint.EnsureDecimalPoint(text);
    }
}

internal sealed class LIntNumber : LNumber
{
    public readonly int number;

    public LIntNumber(int number)
    {
        this.number = number;
    }

    public override string ToPrintString(int flags) => number.ToString(CultureInfo.InvariantCulture);

    public override bool Equals(object o)
    {
        if (o is LIntNumber li)
        {
            return number == li.number;
        }
        if (o is LNumber ln)
        {
            return Value() == ln.Value();
        }
        return false;
    }

    public override int GetHashCode() => number;

    public override double Value() => number;
    public override bool IntegralType() => true;
    public override long Bits() => number;
}

internal sealed class LLongNumber : LNumber
{
    public readonly long number;

    public LLongNumber(long number)
    {
        this.number = number;
    }

    public override string ToPrintString(int flags) => number.ToString(CultureInfo.InvariantCulture);

    public override bool Equals(object o)
    {
        if (o is LLongNumber ll)
        {
            return number == ll.number;
        }
        if (o is LNumber ln)
        {
            return Value() == ln.Value();
        }
        return false;
    }

    public override int GetHashCode() => number.GetHashCode();

    public override double Value() => number;
    public override bool IntegralType() => true;
    public override long Bits() => number;
}

internal static class LNumberPrint
{
    public static string EnsureDecimalPoint(string text)
    {
        int expIndex = text.IndexOfAny(new[] { 'E', 'e' });
        if (expIndex >= 0)
        {
            string mantissa = text.Substring(0, expIndex);
            if (mantissa.IndexOf('.') == -1)
            {
                return mantissa + ".0" + text.Substring(expIndex);
            }
            return text;
        }

        if (text.IndexOf('.') == -1)
        {
            return text + ".0";
        }

        return text;
    }
}
