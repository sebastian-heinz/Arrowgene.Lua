namespace Arrowgene.Lua.Decompiler.Decompile;

public static class PrintFlag
{
    public const int DISASSEMBLER = 0x00000001;
    public const int SHORT = 0x00000002;

    public static bool Test(int flags, int flag) => (flags & flag) != 0;
}
