using System;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Output. An indentation- and paragraph-aware
/// text writer used by the decompiler/disassembler. Tracks the current
/// indentation level (in 2-space units), the column position on the
/// current line, and a paragraph flag that inserts a blank line on the
/// next <see cref="Print(string)"/> after <see cref="Paragraph"/>.
/// </summary>
public sealed class Output
{
    private readonly OutputProvider _out;
    private int indentationLevel = 0;
    private int position = 0;
    private bool start = true;
    private bool paragraph = false;

    public Output() : this(ConsoleOutputProvider.Instance) { }

    public Output(OutputProvider @out)
    {
        _out = @out;
    }

    public void Indent()
    {
        start = true;
        indentationLevel += 2;
    }

    public void Dedent()
    {
        paragraph = false;
        indentationLevel -= 2;
    }

    public void Paragraph()
    {
        paragraph = true;
    }

    public int GetIndentationLevel() => indentationLevel;

    public int GetPosition() => position;

    public void SetIndentationLevel(int indentationLevel)
    {
        this.indentationLevel = indentationLevel;
    }

    private void Start()
    {
        if (position == 0)
        {
            for (int i = indentationLevel; i != 0; i--)
            {
                _out.Print(" ");
                position++;
            }
            if (paragraph && !start)
            {
                paragraph = false;
                _out.PrintLn();
                position = 0;
                Start();
            }
        }
        start = false;
    }

    public void Print(string s)
    {
        Start();
        for (int i = 0; i < s.Length; i++)
        {
            _out.Print((byte)s[i]);
        }
        position += s.Length;
    }

    public void PrintByte(byte b)
    {
        Start();
        _out.Print(b);
        position += 1;
    }

    public void PrintLn()
    {
        Start();
        _out.PrintLn();
        position = 0;
    }

    public void PrintLn(string s)
    {
        Print(s);
        PrintLn();
    }

    public void Finish() => _out.Finish();
}

public abstract class OutputProvider
{
    public abstract void Print(string s);
    public abstract void Print(byte b);
    public abstract void PrintLn();
    public virtual void Finish() { }
}

internal sealed class ConsoleOutputProvider : OutputProvider
{
    public static readonly ConsoleOutputProvider Instance = new ConsoleOutputProvider();
    public override void Print(string s) => Console.Write(s);
    public override void Print(byte b) => Console.OpenStandardOutput().WriteByte(b);
    public override void PrintLn() => Console.WriteLine();
}

/// <summary>
/// Port of unluac.decompile.FileOutputProvider.
/// </summary>
public sealed class FileOutputProvider : OutputProvider
{
    private readonly System.IO.Stream _stream;

    public FileOutputProvider(System.IO.Stream stream)
    {
        _stream = stream;
    }

    public override void Print(string s)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public override void Print(byte b)
    {
        _stream.WriteByte(b);
    }

    public override void PrintLn()
    {
        _stream.WriteByte((byte)'\n');
    }

    public override void Finish()
    {
        _stream.Flush();
    }
}
