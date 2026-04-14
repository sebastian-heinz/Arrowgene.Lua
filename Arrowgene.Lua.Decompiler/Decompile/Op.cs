using System;
using System.Text;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Mirrors unluac's package-private <c>OpV</c> helper: the bitmask values used
/// by each <see cref="Op"/> instance to encode which Lua versions it applies to.
/// </summary>
internal static class OpV
{
    public const int LUA50 = 1;
    public const int LUA51 = 2;
    public const int LUA52 = 4;
    public const int LUA53 = 8;
    public const int LUA54 = 16;
    public const int LUA55 = 32;

    public const int LUA5455 = LUA54 | LUA55;
}

/// <summary>
/// Tag used to identity an <see cref="Op"/> instance. The Java upstream uses an
/// enum directly; in C# we keep a parallel <see cref="OpT"/> enum so the per-op
/// dispatch methods (<c>target</c>, <c>jumpField</c>, ...) can use a clean
/// <c>switch</c> instead of reference comparisons against static fields.
/// </summary>
public enum OpT
{
    // Lua 5.1 base set
    MOVE, LOADK, LOADBOOL, LOADNIL, GETUPVAL, GETGLOBAL, GETTABLE, SETGLOBAL,
    SETUPVAL, SETTABLE, NEWTABLE, SELF, ADD, SUB, MUL, DIV, MOD, POW, UNM, NOT,
    LEN, CONCAT, JMP, EQ, LT, LE, TEST, TESTSET, CALL, TAILCALL, RETURN,
    FORLOOP, FORPREP, TFORLOOP, SETLIST, CLOSE, CLOSURE, VARARG,
    // Lua 5.2 additions
    JMP52, LOADNIL52, LOADKX, GETTABUP, SETTABUP, SETLIST52, TFORCALL,
    TFORLOOP52, EXTRAARG,
    // Lua 5.0 additions
    NEWTABLE50, SETLIST50, SETLISTO, TFORPREP, TEST50,
    // Lua 5.3 additions
    IDIV, BAND, BOR, BXOR, SHL, SHR, BNOT,
    // Lua 5.4 additions
    LOADI, LOADF, LOADFALSE, LFALSESKIP, LOADTRUE,
    GETTABUP54, GETTABLE54, GETI, GETFIELD,
    SETTABUP54, SETTABLE54, SETI, SETFIELD,
    NEWTABLE54, SELF54,
    ADDI, ADDK, SUBK, MULK, MODK, POWK, DIVK, IDIVK,
    BANDK, BORK, BXORK, SHRI, SHLI,
    ADD54, SUB54, MUL54, MOD54, POW54, DIV54, IDIV54,
    BAND54, BOR54, BXOR54, SHL54, SHR54,
    MMBIN, MMBINI, MMBINK, CONCAT54, TBC,
    JMP54, EQ54, LT54, LE54, EQK, EQI, LTI, LEI, GTI, GEI,
    TEST54, TESTSET54,
    TAILCALL54, RETURN54, RETURN0, RETURN1,
    FORLOOP54, FORPREP54, TFORPREP54, TFORCALL54, TFORLOOP54,
    SETLIST54, VARARG54, VARARGPREP,
    // Lua 5.5 additions
    NEWTABLE55, SELF55, FORPREP55, TFORPREP55, SETLIST55, CLOSE55, GETVARG, ERRNNIL,
    // Special
    EXTRABYTE, DEFAULT, DEFAULT54,
}

/// <summary>
/// Port of unluac.decompile.Op. In the upstream Java this is a single ~500 line
/// enum with one constant per Lua opcode. The C# port keeps the per-instance
/// metadata (name, version mask, operand schema) on this sealed class plus an
/// <see cref="OpT"/> tag used by the dispatch methods.
/// </summary>
public sealed class Op
{
    public readonly OpT Type;
    public readonly string Name;
    public readonly int Versions;
    public readonly OperandFormat[] Operands;

    private Op(OpT type, string name, int versions, params OperandFormat[] operands)
    {
        Type = type;
        Name = name;
        Versions = versions;
        Operands = operands;
    }

    public override string ToString() => Type.ToString();

    // --- Instances ------------------------------------------------------------------------
    // Mirrors the upstream enum order: Lua 5.1 base set, then Lua 5.2, Lua 5.0,
    // Lua 5.3, Lua 5.4, Lua 5.5, then specials (EXTRABYTE/DEFAULT/DEFAULT54).

    private const int V_ALL = OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53 | OpV.LUA5455;

    // Lua 5.1 Opcodes
    public static readonly Op MOVE     = new Op(OpT.MOVE,     "move",     V_ALL, OperandFormat.AR, OperandFormat.BR);
    public static readonly Op LOADK    = new Op(OpT.LOADK,    "loadk",    V_ALL, OperandFormat.AR, OperandFormat.BxK);
    public static readonly Op LOADBOOL = new Op(OpT.LOADBOOL, "loadbool", OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.B, OperandFormat.C);
    public static readonly Op LOADNIL  = new Op(OpT.LOADNIL,  "loadnil",  OpV.LUA50 | OpV.LUA51, OperandFormat.AR, OperandFormat.BR);
    public static readonly Op GETUPVAL = new Op(OpT.GETUPVAL, "getupval", V_ALL, OperandFormat.AR, OperandFormat.BU);
    public static readonly Op GETGLOBAL= new Op(OpT.GETGLOBAL,"getglobal",OpV.LUA50 | OpV.LUA51, OperandFormat.AR, OperandFormat.BxK);
    public static readonly Op GETTABLE = new Op(OpT.GETTABLE, "gettable", OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BR, OperandFormat.CRK);
    public static readonly Op SETGLOBAL= new Op(OpT.SETGLOBAL,"setglobal",OpV.LUA50 | OpV.LUA51, OperandFormat.AR, OperandFormat.BxK);
    public static readonly Op SETUPVAL = new Op(OpT.SETUPVAL, "setupval", V_ALL, OperandFormat.AR, OperandFormat.BU);
    public static readonly Op SETTABLE = new Op(OpT.SETTABLE, "settable", OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op NEWTABLE = new Op(OpT.NEWTABLE, "newtable", OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.B, OperandFormat.C);
    public static readonly Op SELF     = new Op(OpT.SELF,     "self",     OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BR, OperandFormat.CRK);
    public static readonly Op ADD      = new Op(OpT.ADD,      "add",      OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op SUB      = new Op(OpT.SUB,      "sub",      OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op MUL      = new Op(OpT.MUL,      "mul",      OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op DIV      = new Op(OpT.DIV,      "div",      OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op MOD      = new Op(OpT.MOD,      "mod",      OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op POW      = new Op(OpT.POW,      "pow",      OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op UNM      = new Op(OpT.UNM,      "unm",      OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BR);
    public static readonly Op NOT      = new Op(OpT.NOT,      "not",      OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BR);
    public static readonly Op LEN      = new Op(OpT.LEN,      "len",      OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BR);
    public static readonly Op CONCAT   = new Op(OpT.CONCAT,   "concat",   OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op JMP      = new Op(OpT.JMP,      "jmp",      OpV.LUA50 | OpV.LUA51, OperandFormat.sBxJ);
    public static readonly Op EQ       = new Op(OpT.EQ,       "eq",       OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.A, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op LT       = new Op(OpT.LT,       "lt",       OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.A, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op LE       = new Op(OpT.LE,       "le",       OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.A, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op TEST     = new Op(OpT.TEST,     "test",     OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.C);
    public static readonly Op TESTSET  = new Op(OpT.TESTSET,  "testset",  OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BR, OperandFormat.C);
    public static readonly Op CALL     = new Op(OpT.CALL,     "call",     V_ALL, OperandFormat.AR, OperandFormat.B, OperandFormat.C);
    public static readonly Op TAILCALL = new Op(OpT.TAILCALL, "tailcall", OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.B);
    public static readonly Op RETURN   = new Op(OpT.RETURN,   "return",   OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.B);
    public static readonly Op FORLOOP  = new Op(OpT.FORLOOP,  "forloop",  OpV.LUA50 | OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.sBxJ);
    public static readonly Op FORPREP  = new Op(OpT.FORPREP,  "forprep",  OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.sBxJ);
    public static readonly Op TFORLOOP = new Op(OpT.TFORLOOP, "tforloop", OpV.LUA50 | OpV.LUA51, OperandFormat.AR, OperandFormat.C);
    public static readonly Op SETLIST  = new Op(OpT.SETLIST,  "setlist",  OpV.LUA51, OperandFormat.AR, OperandFormat.B, OperandFormat.C);
    public static readonly Op CLOSE    = new Op(OpT.CLOSE,    "close",    OpV.LUA50 | OpV.LUA51 | OpV.LUA54, OperandFormat.AR);
    public static readonly Op CLOSURE  = new Op(OpT.CLOSURE,  "closure",  V_ALL, OperandFormat.AR, OperandFormat.BxF);
    public static readonly Op VARARG   = new Op(OpT.VARARG,   "vararg",   OpV.LUA51 | OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.B);

    // Lua 5.2 Opcodes
    public static readonly Op JMP52      = new Op(OpT.JMP52,      "jmp",      OpV.LUA52 | OpV.LUA53, OperandFormat.A, OperandFormat.sBxJ);
    public static readonly Op LOADNIL52  = new Op(OpT.LOADNIL52,  "loadnil",  OpV.LUA52 | OpV.LUA53 | OpV.LUA5455, OperandFormat.AR, OperandFormat.B);
    public static readonly Op LOADKX     = new Op(OpT.LOADKX,     "loadkx",   OpV.LUA52 | OpV.LUA53 | OpV.LUA5455, OperandFormat.AR);
    public static readonly Op GETTABUP   = new Op(OpT.GETTABUP,   "gettabup", OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.BU, OperandFormat.CRK);
    public static readonly Op SETTABUP   = new Op(OpT.SETTABUP,   "settabup", OpV.LUA52 | OpV.LUA53, OperandFormat.AU, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op SETLIST52  = new Op(OpT.SETLIST52,  "setlist",  OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.B, OperandFormat.C);
    public static readonly Op TFORCALL   = new Op(OpT.TFORCALL,   "tforcall", OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.C);
    public static readonly Op TFORLOOP52 = new Op(OpT.TFORLOOP52, "tforloop", OpV.LUA52 | OpV.LUA53, OperandFormat.AR, OperandFormat.sBxJ);
    public static readonly Op EXTRAARG   = new Op(OpT.EXTRAARG,   "extraarg", OpV.LUA52 | OpV.LUA53 | OpV.LUA5455, OperandFormat.Ax);

    // Lua 5.0 Opcodes
    public static readonly Op NEWTABLE50 = new Op(OpT.NEWTABLE50, "newtable", OpV.LUA50, OperandFormat.AR, OperandFormat.B, OperandFormat.C);
    public static readonly Op SETLIST50  = new Op(OpT.SETLIST50,  "setlist",  OpV.LUA50, OperandFormat.AR, OperandFormat.Bx);
    public static readonly Op SETLISTO   = new Op(OpT.SETLISTO,   "setlisto", OpV.LUA50, OperandFormat.AR, OperandFormat.Bx);
    public static readonly Op TFORPREP   = new Op(OpT.TFORPREP,   "tforprep", OpV.LUA50, OperandFormat.AR, OperandFormat.sBxJ);
    public static readonly Op TEST50     = new Op(OpT.TEST50,     "test",     OpV.LUA50, OperandFormat.AR, OperandFormat.BR, OperandFormat.C);

    // Lua 5.3 Opcodes
    public static readonly Op IDIV = new Op(OpT.IDIV, "idiv", OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op BAND = new Op(OpT.BAND, "band", OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op BOR  = new Op(OpT.BOR,  "bor",  OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op BXOR = new Op(OpT.BXOR, "bxor", OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op SHL  = new Op(OpT.SHL,  "shl",  OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op SHR  = new Op(OpT.SHR,  "shr",  OpV.LUA53, OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);
    public static readonly Op BNOT = new Op(OpT.BNOT, "bnot", OpV.LUA53 | OpV.LUA5455, OperandFormat.AR, OperandFormat.BR);

    // Lua 5.4 Opcodes
    public static readonly Op LOADI      = new Op(OpT.LOADI,      "loadi",      OpV.LUA5455, OperandFormat.AR, OperandFormat.sBxI);
    public static readonly Op LOADF      = new Op(OpT.LOADF,      "loadf",      OpV.LUA5455, OperandFormat.AR, OperandFormat.sBxF);
    public static readonly Op LOADFALSE  = new Op(OpT.LOADFALSE,  "loadfalse",  OpV.LUA5455, OperandFormat.AR);
    public static readonly Op LFALSESKIP = new Op(OpT.LFALSESKIP, "lfalseskip", OpV.LUA5455, OperandFormat.AR);
    public static readonly Op LOADTRUE   = new Op(OpT.LOADTRUE,   "loadtrue",   OpV.LUA5455, OperandFormat.AR);
    public static readonly Op GETTABUP54 = new Op(OpT.GETTABUP54, "gettabup",   OpV.LUA5455, OperandFormat.AR, OperandFormat.BU, OperandFormat.CKS);
    public static readonly Op GETTABLE54 = new Op(OpT.GETTABLE54, "gettable",   OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op GETI       = new Op(OpT.GETI,       "geti",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CI);
    public static readonly Op GETFIELD   = new Op(OpT.GETFIELD,   "getfield",   OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CKS);
    public static readonly Op SETTABUP54 = new Op(OpT.SETTABUP54, "settabup",   OpV.LUA5455, OperandFormat.AU, OperandFormat.BK, OperandFormat.CRK54);
    public static readonly Op SETTABLE54 = new Op(OpT.SETTABLE54, "settable",   OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CRK54);
    public static readonly Op SETI       = new Op(OpT.SETI,       "seti",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BI, OperandFormat.CRK54);
    public static readonly Op SETFIELD   = new Op(OpT.SETFIELD,   "setfield",   OpV.LUA5455, OperandFormat.AR, OperandFormat.BKS, OperandFormat.CRK54);
    public static readonly Op NEWTABLE54 = new Op(OpT.NEWTABLE54, "newtable",   OpV.LUA54,   OperandFormat.AR, OperandFormat.B, OperandFormat.C, OperandFormat.k);
    public static readonly Op SELF54     = new Op(OpT.SELF54,     "self",       OpV.LUA54,   OperandFormat.AR, OperandFormat.BR, OperandFormat.CRK54);
    public static readonly Op ADDI       = new Op(OpT.ADDI,       "addi",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CsI);
    public static readonly Op ADDK       = new Op(OpT.ADDK,       "addk",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op SUBK       = new Op(OpT.SUBK,       "subk",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op MULK       = new Op(OpT.MULK,       "mulk",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op MODK       = new Op(OpT.MODK,       "modk",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op POWK       = new Op(OpT.POWK,       "powk",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op DIVK       = new Op(OpT.DIVK,       "divk",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op IDIVK      = new Op(OpT.IDIVK,      "idivk",      OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op BANDK      = new Op(OpT.BANDK,      "bandk",      OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CKI);
    public static readonly Op BORK       = new Op(OpT.BORK,       "bork",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CKI);
    public static readonly Op BXORK      = new Op(OpT.BXORK,      "bxork",      OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CKI);
    public static readonly Op SHRI       = new Op(OpT.SHRI,       "shri",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CsI);
    public static readonly Op SHLI       = new Op(OpT.SHLI,       "shli",       OpV.LUA5455, OperandFormat.AR, OperandFormat.CsI, OperandFormat.BR);
    public static readonly Op ADD54      = new Op(OpT.ADD54,      "add",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op SUB54      = new Op(OpT.SUB54,      "sub",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op MUL54      = new Op(OpT.MUL54,      "mul",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op MOD54      = new Op(OpT.MOD54,      "mod",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op POW54      = new Op(OpT.POW54,      "pow",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op DIV54      = new Op(OpT.DIV54,      "div",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op IDIV54     = new Op(OpT.IDIV54,     "idiv",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op BAND54     = new Op(OpT.BAND54,     "band",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op BOR54      = new Op(OpT.BOR54,      "bor",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op BXOR54     = new Op(OpT.BXOR54,     "bxor",       OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op SHL54      = new Op(OpT.SHL54,      "shl",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op SHR54      = new Op(OpT.SHR54,      "shr",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op MMBIN      = new Op(OpT.MMBIN,      "mmbin",      OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.C);
    public static readonly Op MMBINI     = new Op(OpT.MMBINI,     "mmbini",     OpV.LUA5455, OperandFormat.AR, OperandFormat.BsI, OperandFormat.C, OperandFormat.k);
    public static readonly Op MMBINK     = new Op(OpT.MMBINK,     "mmbink",     OpV.LUA5455, OperandFormat.AR, OperandFormat.BK, OperandFormat.C, OperandFormat.k);
    public static readonly Op CONCAT54   = new Op(OpT.CONCAT54,   "concat",     OpV.LUA5455, OperandFormat.AR, OperandFormat.B);
    public static readonly Op TBC        = new Op(OpT.TBC,        "tbc",        OpV.LUA5455, OperandFormat.AR);
    public static readonly Op JMP54      = new Op(OpT.JMP54,      "jmp",        OpV.LUA5455, OperandFormat.sJ);
    public static readonly Op EQ54       = new Op(OpT.EQ54,       "eq",         OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.k);
    public static readonly Op LT54       = new Op(OpT.LT54,       "lt",         OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.k);
    public static readonly Op LE54       = new Op(OpT.LE54,       "le",         OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.k);
    public static readonly Op EQK        = new Op(OpT.EQK,        "eqk",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BK, OperandFormat.k);
    public static readonly Op EQI        = new Op(OpT.EQI,        "eqi",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BsI, OperandFormat.k, OperandFormat.C);
    public static readonly Op LTI        = new Op(OpT.LTI,        "lti",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BsI, OperandFormat.k, OperandFormat.C);
    public static readonly Op LEI        = new Op(OpT.LEI,        "lei",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BsI, OperandFormat.k, OperandFormat.C);
    public static readonly Op GTI        = new Op(OpT.GTI,        "gti",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BsI, OperandFormat.k, OperandFormat.C);
    public static readonly Op GEI        = new Op(OpT.GEI,        "gei",        OpV.LUA5455, OperandFormat.AR, OperandFormat.BsI, OperandFormat.k, OperandFormat.C);
    public static readonly Op TEST54     = new Op(OpT.TEST54,     "test",       OpV.LUA5455, OperandFormat.AR, OperandFormat.k);
    public static readonly Op TESTSET54  = new Op(OpT.TESTSET54,  "testset",    OpV.LUA5455, OperandFormat.AR, OperandFormat.BR, OperandFormat.k);
    public static readonly Op TAILCALL54 = new Op(OpT.TAILCALL54, "tailcall",   OpV.LUA5455, OperandFormat.AR, OperandFormat.B, OperandFormat.C, OperandFormat.k);
    public static readonly Op RETURN54   = new Op(OpT.RETURN54,   "return",     OpV.LUA5455, OperandFormat.AR, OperandFormat.B, OperandFormat.C, OperandFormat.k);
    public static readonly Op RETURN0    = new Op(OpT.RETURN0,    "return0",    OpV.LUA5455, OperandFormat.AR, OperandFormat.B, OperandFormat.C, OperandFormat.k);
    public static readonly Op RETURN1    = new Op(OpT.RETURN1,    "return1",    OpV.LUA5455, OperandFormat.AR, OperandFormat.B, OperandFormat.C, OperandFormat.k);
    public static readonly Op FORLOOP54  = new Op(OpT.FORLOOP54,  "forloop",    OpV.LUA5455, OperandFormat.AR, OperandFormat.BxJn);
    public static readonly Op FORPREP54  = new Op(OpT.FORPREP54,  "forprep",    OpV.LUA54,   OperandFormat.AR, OperandFormat.BxJ);
    public static readonly Op TFORPREP54 = new Op(OpT.TFORPREP54, "tforprep",   OpV.LUA5455, OperandFormat.AR, OperandFormat.BxJ);
    public static readonly Op TFORCALL54 = new Op(OpT.TFORCALL54, "tforcall",   OpV.LUA5455, OperandFormat.AR, OperandFormat.C);
    public static readonly Op TFORLOOP54 = new Op(OpT.TFORLOOP54, "tforloop",   OpV.LUA54,   OperandFormat.AR, OperandFormat.BxJn);
    public static readonly Op SETLIST54  = new Op(OpT.SETLIST54,  "setlist",    OpV.LUA54,   OperandFormat.AR, OperandFormat.B, OperandFormat.C, OperandFormat.k);
    public static readonly Op VARARG54   = new Op(OpT.VARARG54,   "vararg",     OpV.LUA5455, OperandFormat.AR, OperandFormat.C);
    public static readonly Op VARARGPREP = new Op(OpT.VARARGPREP, "varargprep", OpV.LUA5455, OperandFormat.A);

    // Lua 5.5 Opcodes
    public static readonly Op NEWTABLE55 = new Op(OpT.NEWTABLE55, "newtable", OpV.LUA55, OperandFormat.AR, OperandFormat.B, OperandFormat.C, OperandFormat.k);
    public static readonly Op SELF55     = new Op(OpT.SELF55,     "self",     OpV.LUA55, OperandFormat.AR, OperandFormat.BR, OperandFormat.CK);
    public static readonly Op FORPREP55  = new Op(OpT.FORPREP55,  "forprep",  OpV.LUA55, OperandFormat.AR, OperandFormat.BxJ);
    public static readonly Op TFORPREP55 = new Op(OpT.TFORPREP55, "tforprep", OpV.LUA55, OperandFormat.AR, OperandFormat.BxJ);
    public static readonly Op SETLIST55  = new Op(OpT.SETLIST55,  "setlist",  OpV.LUA55, OperandFormat.AR, OperandFormat.vB, OperandFormat.vC, OperandFormat.k);
    public static readonly Op CLOSE55    = new Op(OpT.CLOSE55,    "close",    OpV.LUA55, OperandFormat.AR);
    public static readonly Op GETVARG    = new Op(OpT.GETVARG,    "getvarg",  OpV.LUA55, OperandFormat.AR, OperandFormat.BR, OperandFormat.CR);
    public static readonly Op ERRNNIL    = new Op(OpT.ERRNNIL,    "errnnil",  OpV.LUA55, OperandFormat.AR, OperandFormat.BxK);

    // Special
    public static readonly Op EXTRABYTE = new Op(OpT.EXTRABYTE, "extrabyte", V_ALL, OperandFormat.x);

    public static readonly Op DEFAULT = new Op(OpT.DEFAULT, "default", 0,
        OperandFormat.AR, OperandFormat.BRK, OperandFormat.CRK);

    public static readonly Op DEFAULT54 = new Op(OpT.DEFAULT54, "default", 0,
        OperandFormat.AR, OperandFormat.BR, OperandFormat.C, OperandFormat.k);

    // --- Methods --------------------------------------------------------------------------

    /// <summary>
    /// SETLIST sometimes uses an extra byte without tagging it. The value in
    /// the extra byte can be detected as any other opcode unless it is recognized.
    /// </summary>
    public bool HasExtraByte(int codepoint, CodeExtract ex)
    {
        if (this == SETLIST)
        {
            return ex.C.Extract(codepoint) == 0;
        }
        return false;
    }

    public int JumpField(int codepoint, CodeExtract ex)
    {
        switch (Type)
        {
            case OpT.FORPREP54:
            case OpT.FORPREP55:
            case OpT.TFORPREP54:
            case OpT.TFORPREP55:
                return ex.Bx.Extract(codepoint);
            case OpT.FORLOOP54:
            case OpT.TFORLOOP54:
                return -ex.Bx.Extract(codepoint);
            case OpT.JMP:
            case OpT.FORLOOP:
            case OpT.FORPREP:
            case OpT.JMP52:
            case OpT.TFORLOOP52:
            case OpT.TFORPREP:
                return ex.sBx.Extract(codepoint);
            case OpT.JMP54:
                return ex.sJ.Extract(codepoint);
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>Is this op the standard JMP instruction in its opmap?</summary>
    public bool IsJmp()
    {
        switch (Type)
        {
            case OpT.JMP:
            case OpT.JMP52:
            case OpT.JMP54:
                return true;
            default:
                return false;
        }
    }

    public bool IsCondition()
    {
        switch (Type)
        {
            case OpT.TEST50:
            case OpT.TEST: case OpT.TESTSET:
            case OpT.TEST54: case OpT.TESTSET54:
            case OpT.EQ: case OpT.LT: case OpT.LE:
            case OpT.EQ54: case OpT.LT54: case OpT.LE54:
            case OpT.EQK: case OpT.EQI:
            case OpT.LTI: case OpT.LEI: case OpT.GTI: case OpT.GEI:
                return true;
            default:
                return false;
        }
    }

    public bool HasJump()
    {
        for (int i = 0; i < Operands.Length; ++i)
        {
            OperandFormat.Format format = Operands[i].format;
            if (format == OperandFormat.Format.JUMP || format == OperandFormat.Format.JUMP_NEGATIVE)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the target register of the instruction at the given line, or -1
    /// if the instruction does not have a unique target.
    /// </summary>
    public int Target(int codepoint, CodeExtract ex)
    {
        switch (Type)
        {
            case OpT.MOVE:
            case OpT.LOADI: case OpT.LOADF: case OpT.LOADK: case OpT.LOADKX:
            case OpT.LOADBOOL: case OpT.LOADFALSE: case OpT.LFALSESKIP: case OpT.LOADTRUE:
            case OpT.GETUPVAL:
            case OpT.GETTABUP: case OpT.GETTABUP54:
            case OpT.GETGLOBAL:
            case OpT.GETTABLE: case OpT.GETTABLE54: case OpT.GETI: case OpT.GETFIELD:
            case OpT.NEWTABLE50: case OpT.NEWTABLE: case OpT.NEWTABLE54: case OpT.NEWTABLE55:
            case OpT.ADD: case OpT.SUB: case OpT.MUL: case OpT.DIV: case OpT.IDIV: case OpT.MOD:
            case OpT.POW: case OpT.BAND: case OpT.BOR: case OpT.BXOR: case OpT.SHL: case OpT.SHR:
            case OpT.ADD54: case OpT.SUB54: case OpT.MUL54: case OpT.DIV54: case OpT.IDIV54:
            case OpT.MOD54: case OpT.POW54: case OpT.BAND54: case OpT.BOR54: case OpT.BXOR54:
            case OpT.SHL54: case OpT.SHR54:
            case OpT.ADDK: case OpT.SUBK: case OpT.MULK: case OpT.DIVK: case OpT.IDIVK:
            case OpT.MODK: case OpT.POWK: case OpT.BANDK: case OpT.BORK: case OpT.BXORK:
            case OpT.ADDI: case OpT.SHLI: case OpT.SHRI:
            case OpT.UNM: case OpT.NOT: case OpT.LEN: case OpT.BNOT:
            case OpT.CONCAT: case OpT.CONCAT54:
            case OpT.CLOSURE:
            case OpT.TEST50: case OpT.TESTSET: case OpT.TESTSET54:
            case OpT.GETVARG:
                return ex.A.Extract(codepoint);
            case OpT.MMBIN: case OpT.MMBINI: case OpT.MMBINK:
                return -1; // depends on previous instruction
            case OpT.LOADNIL:
                if (ex.A.Extract(codepoint) == ex.B.Extract(codepoint))
                {
                    return ex.A.Extract(codepoint);
                }
                else
                {
                    return -1;
                }
            case OpT.LOADNIL52:
                if (ex.B.Extract(codepoint) == 0)
                {
                    return ex.A.Extract(codepoint);
                }
                else
                {
                    return -1;
                }
            case OpT.SETGLOBAL:
            case OpT.SETUPVAL:
            case OpT.SETTABUP: case OpT.SETTABUP54:
            case OpT.SETTABLE: case OpT.SETTABLE54: case OpT.SETI: case OpT.SETFIELD:
            case OpT.JMP: case OpT.JMP52: case OpT.JMP54:
            case OpT.TAILCALL: case OpT.TAILCALL54:
            case OpT.RETURN: case OpT.RETURN54: case OpT.RETURN0: case OpT.RETURN1:
            case OpT.FORLOOP: case OpT.FORLOOP54:
            case OpT.FORPREP: case OpT.FORPREP54: case OpT.FORPREP55:
            case OpT.TFORPREP: case OpT.TFORPREP54: case OpT.TFORPREP55:
            case OpT.TFORCALL: case OpT.TFORCALL54:
            case OpT.TFORLOOP: case OpT.TFORLOOP52: case OpT.TFORLOOP54:
            case OpT.TBC:
            case OpT.CLOSE: case OpT.CLOSE55:
            case OpT.EXTRAARG:
            case OpT.SELF: case OpT.SELF54: case OpT.SELF55:
            case OpT.EQ: case OpT.LT: case OpT.LE:
            case OpT.EQ54: case OpT.LT54: case OpT.LE54:
            case OpT.EQK: case OpT.EQI: case OpT.LTI: case OpT.LEI: case OpT.GTI: case OpT.GEI:
            case OpT.TEST: case OpT.TEST54:
            case OpT.SETLIST50: case OpT.SETLISTO: case OpT.SETLIST: case OpT.SETLIST52:
            case OpT.SETLIST54: case OpT.SETLIST55:
            case OpT.ERRNNIL:
            case OpT.VARARGPREP:
                return -1;
            case OpT.CALL:
            {
                int a = ex.A.Extract(codepoint);
                int c = ex.C.Extract(codepoint);
                if (c == 1 || c == 2)
                {
                    return a;
                }
                else
                {
                    return -1;
                }
            }
            case OpT.VARARG:
            {
                int a = ex.A.Extract(codepoint);
                int b = ex.B.Extract(codepoint);
                if (b == 1 || b == 2)
                {
                    return a;
                }
                else
                {
                    return -1;
                }
            }
            case OpT.VARARG54:
            {
                int a = ex.A.Extract(codepoint);
                int c = ex.C.Extract(codepoint);
                if (c == 1 || c == 2)
                {
                    return a;
                }
                else
                {
                    return -1;
                }
            }
            case OpT.EXTRABYTE:
                return -1;
            case OpT.DEFAULT:
            case OpT.DEFAULT54:
                throw new InvalidOperationException();
            default:
                throw new InvalidOperationException(Type.ToString());
        }
    }

    private static string FixedOperand(int field) => field.ToString();
    private static string RegisterOperand(int field) => "r" + field;
    private static string UpvalueOperand(int field) => "u" + field;
    private static string ConstantOperand(int field) => "k" + field;
    private static string FunctionOperand(int field) => "f" + field;

    public string CodePointToString(int flags, LFunction function, int codepoint,
                                     CodeExtract ex, string label, bool upvalue)
    {
        return ToStringHelper(flags, function, Name, Operands, codepoint, ex, label, upvalue);
    }

    public static string DefaultToString(int flags, LFunction function, int codepoint,
                                          Version version, CodeExtract ex, bool upvalue)
    {
        return ToStringHelper(flags, function,
            string.Format("op{0:D2}", ex.op.Extract(codepoint)),
            version.GetDefaultOp().Operands, codepoint, ex, null, upvalue);
    }

    private static string ToStringHelper(int flags, LFunction function, string name,
                                          OperandFormat[] operands, int codepoint,
                                          CodeExtract ex, string label, bool upvalue)
    {
        int constant = -1;
        const int width = 10;
        StringBuilder b = new StringBuilder();
        b.Append(name);
        for (int i = 0; i < width - name.Length; i++)
        {
            b.Append(' ');
        }
        int slot = CodeExtract.BITFIELD_OPCODE;
        string[] parameters = new string[operands.Length];
        for (int i = 0; i < operands.Length; ++i)
        {
            CodeExtract.Field field = ex.GetField(operands[i].field);
            slot |= field.slot;
            int x = field.Extract(codepoint);
            switch (operands[i].format)
            {
                case OperandFormat.Format.IMMEDIATE_INTEGER:
                case OperandFormat.Format.IMMEDIATE_FLOAT:
                case OperandFormat.Format.RAW:
                    parameters[i] = FixedOperand(x);
                    break;
                case OperandFormat.Format.IMMEDIATE_SIGNED_INTEGER:
                    parameters[i] = FixedOperand(x - field.Max() / 2);
                    break;
                case OperandFormat.Format.REGISTER:
                    parameters[i] = RegisterOperand(x);
                    break;
                case OperandFormat.Format.UPVALUE:
                    parameters[i] = UpvalueOperand(x);
                    break;
                case OperandFormat.Format.REGISTER_K:
                    if (ex.IsK(x))
                    {
                        constant = ex.GetK(x);
                        parameters[i] = ConstantOperand(constant);
                    }
                    else
                    {
                        parameters[i] = RegisterOperand(x);
                    }
                    break;
                case OperandFormat.Format.REGISTER_K54:
                    if (ex.k.Extract(codepoint) != 0)
                    {
                        constant = x;
                        parameters[i] = ConstantOperand(x);
                    }
                    else
                    {
                        parameters[i] = RegisterOperand(x);
                    }
                    break;
                case OperandFormat.Format.CONSTANT:
                case OperandFormat.Format.CONSTANT_INTEGER:
                case OperandFormat.Format.CONSTANT_STRING:
                    constant = x;
                    parameters[i] = ConstantOperand(x);
                    break;
                case OperandFormat.Format.FUNCTION:
                    parameters[i] = FunctionOperand(x);
                    break;
                case OperandFormat.Format.JUMP:
                    if (label != null)
                    {
                        parameters[i] = label;
                    }
                    else
                    {
                        parameters[i] = FixedOperand(x + operands[i].offset);
                    }
                    break;
                case OperandFormat.Format.JUMP_NEGATIVE:
                    if (label != null)
                    {
                        parameters[i] = label;
                    }
                    else
                    {
                        parameters[i] = FixedOperand(-x);
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
        foreach (string parameter in parameters)
        {
            b.Append(' ');
            for (int i = 0; i < 5 - parameter.Length; i++)
            {
                b.Append(' ');
            }
            b.Append(parameter);
        }

        while (true)
        {
            CodeExtract.Field extra = ex.GetFieldForSlot(slot);
            if (extra == null) break;
            slot |= extra.slot;
            int x = extra.Extract(codepoint);
            if (x != 0)
            {
                b.Append(' ');
                string parameter = extra.DefaultName() + "= " + FixedOperand(x);
                for (int i = 0; i < 5 - parameter.Length; i++)
                {
                    b.Append(' ');
                }
                b.Append(parameter);
            }
        }

        if (upvalue)
        {
            b.Append(" ; upvalue declaration");
        }
        else if (function != null && constant >= 0)
        {
            b.Append(" ; ");
            b.Append(ConstantOperand(constant));
            if (constant < function.constants.Length)
            {
                b.Append(" = ");
                b.Append(function.constants[constant].ToPrintString(flags | PrintFlag.SHORT));
            }
            else
            {
                b.Append(" out of range");
            }
        }
        return b.ToString();
    }
}
