using System;
using System.IO;
using Arrowgene.Lua.Decompiler.Decompile;

namespace Arrowgene.Lua.Decompiler;

/// <summary>
/// Port of unluac.Configuration. Flat state bag for CLI-style options.
/// </summary>
public sealed class Configuration
{
    public enum Mode
    {
        DECOMPILE,
        DISASSEMBLE,
        ASSEMBLE,
        HELP,
        VERSION,
    }

    public enum VariableMode
    {
        NODEBUG,
        DEFAULT,
        FINDER,
    }

    public bool rawstring;
    public Mode mode;
    public VariableMode variable;
    public bool strict_scope;
    public bool luaj;
    public string typemap;
    public string opmap;
    public string output;

    public Configuration()
    {
        rawstring = false;
        mode = Mode.DECOMPILE;
        variable = VariableMode.DEFAULT;
        strict_scope = false;
        luaj = false;
        opmap = null;
        output = null;
    }

    public Configuration(Configuration other)
    {
        rawstring = other.rawstring;
        mode = other.mode;
        variable = other.variable;
        strict_scope = other.strict_scope;
        opmap = other.opmap;
        output = other.output;
    }

    public Output GetOutput()
    {
        if (output != null)
        {
            try
            {
                FileStream fs = new FileStream(output, FileMode.Create, FileAccess.Write);
                return new Output(new FileOutputProvider(fs));
            }
            catch (IOException e)
            {
                Main.Error(e.Message, false);
                return null;
            }
        }

        return new Output();
    }
}
