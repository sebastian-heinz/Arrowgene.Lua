using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.BList. A length-prefixed list of parse-layer objects.
/// </summary>
public sealed class BList<T> : BObject where T : BObject
{
    public readonly BInteger length;
    private readonly List<T> _values;

    public BList(BInteger length, List<T> values)
    {
        this.length = length;
        _values = values;
    }

    public T Get(int index) => _values[index];

    public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();

    public T[] AsArray(T[] array)
    {
        int i = 0;
        length.Iterate(() =>
        {
            array[i] = _values[i];
            i++;
        });
        return array;
    }
}
