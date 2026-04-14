using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Util;

/// <summary>
/// Direct port of unluac.util.Stack. Uses indexable underlying storage so that peek(i)
/// can index from the top, which java.util.Stack / System.Collections.Generic.Stack do not
/// support cleanly.
/// </summary>
public class Stack<T>
{
    private readonly List<T> _data;

    public Stack()
    {
        _data = new List<T>();
    }

    public bool IsEmpty()
    {
        return _data.Count == 0;
    }

    public T Peek()
    {
        return _data[_data.Count - 1];
    }

    public T Peek(int i)
    {
        return _data[_data.Count - 1 - i];
    }

    public T Pop()
    {
        int idx = _data.Count - 1;
        T item = _data[idx];
        _data.RemoveAt(idx);
        return item;
    }

    public void Push(T item)
    {
        if (_data.Count > 65536)
        {
            throw new System.IndexOutOfRangeException("Trying to push more than 65536 items!");
        }
        _data.Add(item);
    }

    public int Size()
    {
        return _data.Count;
    }

    public void Reverse()
    {
        _data.Reverse();
    }
}
