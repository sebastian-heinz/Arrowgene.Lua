using System;
using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.TableLiteral. A table constructor
/// (<c>{ ... }</c>) - holds the entries gathered from a SETLIST/SETTABLE
/// run, classifies the table shape (pure list, pure object, or mixed),
/// and decides whether to emit it on a single line or one entry per line
/// based on entry count and brevity.
/// </summary>
public class TableLiteral : Expression
{
    public sealed class Entry : IComparable<Entry>
    {
        public readonly Expression Key;
        public readonly Expression Value;
        public readonly bool IsList;
        public readonly int Timestamp;
        internal int Sequence;
        internal bool Hash;

        public Entry(Expression key, Expression value, bool isList, int timestamp)
        {
            Key = key;
            Value = value;
            IsList = isList;
            Timestamp = timestamp;
        }

        public int CompareTo(Entry other)
        {
            int timestamp = Timestamp.CompareTo(other.Timestamp);
            if (timestamp != 0)
            {
                return timestamp;
            }
            return Sequence.CompareTo(other.Sequence);
        }
    }

    private readonly List<Entry> entries;

    private bool isObject = true;
    private bool isList = true;
    private int listLength = 1;

    private readonly int hashSize;
    private int hashCount;

    public TableLiteral(int arraySize, int hashSize)
        : base(PRECEDENCE_ATOMIC)
    {
        entries = new List<Entry>(arraySize + hashSize);
        this.hashSize = hashSize;
        hashCount = 0;
    }

    public override void Walk(Walker w)
    {
        entries.Sort();
        w.VisitExpression(this);
        bool lastEntry = false;
        foreach (Entry entry in entries)
        {
            entry.Key.Walk(w);
            if (!lastEntry)
            {
                entry.Value.Walk(w);
                if (entry.Value.IsMultiple())
                {
                    lastEntry = true;
                }
            }
        }
    }

    public override int GetConstantIndex()
    {
        int index = -1;
        foreach (Entry entry in entries)
        {
            int k = entry.Key.GetConstantIndex();
            int v = entry.Value.GetConstantIndex();
            if (k > index) index = k;
            if (v > index) index = v;
        }
        return index;
    }

    public override void Print(Decompiler d, Output @out)
    {
        listLength = 1;
        if (entries.Count == 0)
        {
            @out.Print("{}");
        }
        else
        {
            bool lineBreak = isList && entries.Count > 5 || isObject && entries.Count > 2 || !isObject;
            if (!lineBreak)
            {
                foreach (Entry entry in entries)
                {
                    Expression value = entry.Value;
                    if (!value.IsBrief())
                    {
                        lineBreak = true;
                        break;
                    }
                }
            }
            @out.Print("{");
            if (lineBreak)
            {
                @out.PrintLn();
                @out.Indent();
            }
            PrintEntry(d, 0, @out);
            if (!entries[0].Value.IsMultiple())
            {
                for (int index = 1; index < entries.Count; index++)
                {
                    @out.Print(",");
                    if (lineBreak)
                    {
                        @out.PrintLn();
                    }
                    else
                    {
                        @out.Print(" ");
                    }
                    PrintEntry(d, index, @out);
                    if (entries[index].Value.IsMultiple())
                    {
                        break;
                    }
                }
            }
            if (lineBreak)
            {
                @out.PrintLn();
                @out.Dedent();
            }
            @out.Print("}");
        }
    }

    private void PrintEntry(Decompiler d, int index, Output @out)
    {
        Entry entry = entries[index];
        Expression key = entry.Key;
        Expression value = entry.Value;
        bool isList = entry.IsList;
        bool multiple = index + 1 >= entries.Count || value.IsMultiple();
        if (isList && key.IsInteger() && listLength == key.AsInteger())
        {
            if (multiple)
            {
                value.PrintMultiple(d, @out);
            }
            else
            {
                value.Print(d, @out);
            }
            listLength++;
        }
        else if (entry.Hash /*isObject && key.IsIdentifier()*/)
        {
            @out.Print(key.AsName());
            @out.Print(" = ");
            value.Print(d, @out);
        }
        else
        {
            @out.Print("[");
            key.PrintBraced(d, @out);
            @out.Print("] = ");
            value.Print(d, @out);
        }
    }

    public override bool IsTableLiteral() => true;

    public override bool IsUngrouped() => true;

    public override bool IsNewEntryAllowed() => true;

    public override void AddEntry(Entry entry)
    {
        if (hashCount < hashSize && entry.Key.IsIdentifier())
        {
            entry.Hash = true;
            hashCount++;
        }
        entry.Sequence = entries.Count;
        entries.Add(entry);
        isObject = isObject && (entry.IsList || entry.Key.IsIdentifier());
        isList = isList && entry.IsList;
    }

    public override bool IsBrief() => false;
}
