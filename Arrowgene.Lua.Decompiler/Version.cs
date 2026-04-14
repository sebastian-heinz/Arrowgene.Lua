using System;
using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler;

/// <summary>
/// Port of unluac.Version. The keystone class: a per-(major,minor) bundle of
/// settings and parser factories that drive the rest of the decompiler.
/// Direct port from tehtmi's Version.java.
/// </summary>
public sealed class Version
{
    public static Version GetVersion(Configuration config, int major, int minor)
    {
        return new Version(config, major, minor);
    }

    public sealed class Setting<T>
    {
        private readonly T _value;

        public Setting(T value)
        {
            _value = value;
        }

        public T Get() => _value;
    }

    public enum Maybe
    {
        NO,
        MAYBE,
        YES,
    }

    public enum VarArgType
    {
        ARG,
        HYBRID,
        ELLIPSIS,
        NAMED,
    }

    public enum HeaderType
    {
        LUA50,
        LUA51,
        LUA52,
        LUA53,
        LUA54,
        LUA55,
    }

    public enum StringType
    {
        LUA50,
        LUA53,
        LUA54,
        LUA55,
    }

    public enum UpvalueType
    {
        LUA50,
        LUA54,
    }

    public enum FunctionType
    {
        LUA50,
        LUA51,
        LUA52,
        LUA53,
        LUA54,
        LUA55,
    }

    public enum TypeMapType
    {
        LUA50,
        LUA52,
        LUA53,
        LUA54,
    }

    public enum OpcodeMapType
    {
        LUA50,
        LUA51,
        LUA52,
        LUA53,
        LUA54,
        LUA55,
    }

    public enum UpvalueDeclarationType
    {
        INLINE,
        HEADER,
    }

    public enum InstructionFormat
    {
        LUA50,
        LUA51,
        LUA54,
    }

    public enum WhileFormat
    {
        TOP_CONDITION,
        BOTTOM_CONDITION,
    }

    public enum CloseSemantics
    {
        DEFAULT,
        JUMP,
        LUA54,
    }

    public enum ListLengthMode
    {
        /// <summary>Negative is illegal.</summary>
        STRICT,
        /// <summary>Negative treated as zero.</summary>
        ALLOW_NEGATIVE,
        /// <summary>List length is already known; only accept 0 or else ignore.</summary>
        IGNORE,
    }

    public readonly Setting<VarArgType> varargtype;
    public readonly Setting<bool> useupvaluecountinheader;
    public readonly Setting<InstructionFormat> instructionformat;
    public readonly Setting<int?> outerblockscopeadjustment;
    public readonly Setting<bool> extendedrepeatscope;
    public readonly Setting<CloseSemantics> closesemantics;
    public readonly Setting<UpvalueDeclarationType> upvaluedeclarationtype;
    public readonly Setting<Op> fortarget;
    public readonly Setting<Op> tfortarget;
    public readonly Setting<WhileFormat> whileformat;
    public readonly Setting<bool> allowpreceedingsemicolon;
    public readonly Setting<bool> usenestinglongstrings;
    public readonly Setting<string> environmenttable;
    public readonly Setting<bool> useifbreakrewrite;
    public readonly Setting<Maybe> useifgotorewrite;
    public readonly Setting<bool> usegoto;
    public readonly Setting<bool> usedeadclose;
    public readonly Setting<int?> rkoffset;
    public readonly Setting<bool> allownegativeint;
    public readonly Setting<ListLengthMode> constantslengthmode;
    public readonly Setting<ListLengthMode> functionslengthmode;
    public readonly Setting<ListLengthMode> locallengthmode;
    public readonly Setting<ListLengthMode> upvaluelengthmode;
    public readonly Setting<bool> useglobaldecl;

    private readonly int _major;
    private readonly int _minor;
    private readonly string _name;
    private readonly HashSet<string> _reservedWords;
    private readonly LHeaderType _lheadertype;
    private readonly LStringType _lstringtype;
    private readonly LConstantType _lconstanttype;
    private readonly LUpvalueType _lupvaluetype;
    private readonly LFunctionType _lfunctiontype;
    private readonly TypeMap _typemap;
    private readonly OpcodeMap _opcodemap;
    private readonly Op _defaultop;

    private Version(Configuration config, int major, int minor)
    {
        HeaderType headertype;
        StringType stringtype;
        UpvalueType upvaluetype;
        FunctionType functiontype;
        TypeMapType typemap;
        OpcodeMapType opcodemap;
        _major = major;
        _minor = minor;
        _name = major + "." + minor;
        bool luaj = config.luaj;
        if (major == 5 && minor >= 0 && minor <= 5)
        {
            switch (minor)
            {
                case 0:
                    varargtype = new Setting<VarArgType>(VarArgType.ARG);
                    useupvaluecountinheader = new Setting<bool>(false);
                    headertype = HeaderType.LUA50;
                    stringtype = StringType.LUA50;
                    upvaluetype = UpvalueType.LUA50;
                    functiontype = FunctionType.LUA50;
                    typemap = TypeMapType.LUA50;
                    opcodemap = OpcodeMapType.LUA50;
                    _defaultop = Op.DEFAULT;
                    instructionformat = new Setting<InstructionFormat>(InstructionFormat.LUA50);
                    outerblockscopeadjustment = new Setting<int?>(-1);
                    extendedrepeatscope = new Setting<bool>(true);
                    closesemantics = new Setting<CloseSemantics>(CloseSemantics.DEFAULT);
                    upvaluedeclarationtype = new Setting<UpvalueDeclarationType>(UpvalueDeclarationType.INLINE);
                    fortarget = new Setting<Op>(Op.FORLOOP);
                    tfortarget = new Setting<Op>(null);
                    whileformat = new Setting<WhileFormat>(WhileFormat.BOTTOM_CONDITION);
                    allowpreceedingsemicolon = new Setting<bool>(false);
                    usenestinglongstrings = new Setting<bool>(true);
                    environmenttable = new Setting<string>(null);
                    useifbreakrewrite = new Setting<bool>(false);
                    useifgotorewrite = new Setting<Maybe>(Maybe.NO);
                    usegoto = new Setting<bool>(false);
                    usedeadclose = new Setting<bool>(false);
                    rkoffset = new Setting<int?>(250);
                    allownegativeint = new Setting<bool>(false);
                    constantslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    functionslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    locallengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    upvaluelengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    useglobaldecl = new Setting<bool>(false);
                    break;
                case 1:
                    varargtype = new Setting<VarArgType>(VarArgType.HYBRID);
                    useupvaluecountinheader = new Setting<bool>(false);
                    headertype = HeaderType.LUA51;
                    stringtype = StringType.LUA50;
                    upvaluetype = UpvalueType.LUA50;
                    functiontype = FunctionType.LUA51;
                    typemap = TypeMapType.LUA50;
                    opcodemap = OpcodeMapType.LUA51;
                    _defaultop = Op.DEFAULT;
                    instructionformat = new Setting<InstructionFormat>(InstructionFormat.LUA51);
                    outerblockscopeadjustment = new Setting<int?>(-1);
                    extendedrepeatscope = new Setting<bool>(false);
                    closesemantics = new Setting<CloseSemantics>(CloseSemantics.DEFAULT);
                    upvaluedeclarationtype = new Setting<UpvalueDeclarationType>(UpvalueDeclarationType.INLINE);
                    fortarget = new Setting<Op>(null);
                    tfortarget = new Setting<Op>(Op.TFORLOOP);
                    whileformat = new Setting<WhileFormat>(WhileFormat.TOP_CONDITION);
                    allowpreceedingsemicolon = new Setting<bool>(false);
                    usenestinglongstrings = new Setting<bool>(false);
                    environmenttable = new Setting<string>(null);
                    useifbreakrewrite = new Setting<bool>(false);
                    useifgotorewrite = new Setting<Maybe>(Maybe.NO);
                    usegoto = new Setting<bool>(false);
                    usedeadclose = new Setting<bool>(false);
                    rkoffset = new Setting<int?>(256);
                    allownegativeint = new Setting<bool>(luaj);
                    constantslengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    functionslengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    locallengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    upvaluelengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    useglobaldecl = new Setting<bool>(false);
                    break;
                case 2:
                    varargtype = new Setting<VarArgType>(VarArgType.ELLIPSIS);
                    useupvaluecountinheader = new Setting<bool>(false);
                    headertype = HeaderType.LUA52;
                    stringtype = StringType.LUA50;
                    upvaluetype = UpvalueType.LUA50;
                    functiontype = FunctionType.LUA52;
                    typemap = TypeMapType.LUA52;
                    opcodemap = OpcodeMapType.LUA52;
                    _defaultop = Op.DEFAULT;
                    instructionformat = new Setting<InstructionFormat>(InstructionFormat.LUA51);
                    outerblockscopeadjustment = new Setting<int?>(0);
                    extendedrepeatscope = new Setting<bool>(false);
                    closesemantics = new Setting<CloseSemantics>(CloseSemantics.JUMP);
                    upvaluedeclarationtype = new Setting<UpvalueDeclarationType>(UpvalueDeclarationType.HEADER);
                    fortarget = new Setting<Op>(null);
                    tfortarget = new Setting<Op>(Op.TFORCALL);
                    whileformat = new Setting<WhileFormat>(WhileFormat.TOP_CONDITION);
                    allowpreceedingsemicolon = new Setting<bool>(true);
                    usenestinglongstrings = new Setting<bool>(false);
                    environmenttable = new Setting<string>("_ENV");
                    useifbreakrewrite = new Setting<bool>(true);
                    useifgotorewrite = new Setting<Maybe>(Maybe.YES);
                    usegoto = new Setting<bool>(true);
                    usedeadclose = new Setting<bool>(false);
                    rkoffset = new Setting<int?>(256);
                    allownegativeint = new Setting<bool>(luaj);
                    constantslengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    functionslengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    locallengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    upvaluelengthmode = new Setting<ListLengthMode>(luaj ? ListLengthMode.ALLOW_NEGATIVE : ListLengthMode.STRICT);
                    useglobaldecl = new Setting<bool>(false);
                    break;
                case 3:
                    varargtype = new Setting<VarArgType>(VarArgType.ELLIPSIS);
                    useupvaluecountinheader = new Setting<bool>(true);
                    headertype = HeaderType.LUA53;
                    stringtype = StringType.LUA53;
                    upvaluetype = UpvalueType.LUA50;
                    functiontype = FunctionType.LUA53;
                    typemap = TypeMapType.LUA53;
                    opcodemap = OpcodeMapType.LUA53;
                    _defaultop = Op.DEFAULT;
                    instructionformat = new Setting<InstructionFormat>(InstructionFormat.LUA51);
                    outerblockscopeadjustment = new Setting<int?>(0);
                    extendedrepeatscope = new Setting<bool>(false);
                    closesemantics = new Setting<CloseSemantics>(CloseSemantics.JUMP);
                    upvaluedeclarationtype = new Setting<UpvalueDeclarationType>(UpvalueDeclarationType.HEADER);
                    fortarget = new Setting<Op>(null);
                    tfortarget = new Setting<Op>(Op.TFORCALL);
                    whileformat = new Setting<WhileFormat>(WhileFormat.TOP_CONDITION);
                    allowpreceedingsemicolon = new Setting<bool>(true);
                    usenestinglongstrings = new Setting<bool>(false);
                    environmenttable = new Setting<string>("_ENV");
                    useifbreakrewrite = new Setting<bool>(true);
                    useifgotorewrite = new Setting<Maybe>(Maybe.YES);
                    usegoto = new Setting<bool>(true);
                    usedeadclose = new Setting<bool>(false);
                    rkoffset = new Setting<int?>(256);
                    allownegativeint = new Setting<bool>(true);
                    constantslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    functionslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    locallengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    upvaluelengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    useglobaldecl = new Setting<bool>(false);
                    break;
                case 4:
                    varargtype = new Setting<VarArgType>(VarArgType.ELLIPSIS);
                    useupvaluecountinheader = new Setting<bool>(true);
                    headertype = HeaderType.LUA54;
                    stringtype = StringType.LUA54;
                    upvaluetype = UpvalueType.LUA54;
                    functiontype = FunctionType.LUA54;
                    typemap = TypeMapType.LUA54;
                    opcodemap = OpcodeMapType.LUA54;
                    _defaultop = Op.DEFAULT54;
                    instructionformat = new Setting<InstructionFormat>(InstructionFormat.LUA54);
                    outerblockscopeadjustment = new Setting<int?>(0);
                    extendedrepeatscope = new Setting<bool>(false);
                    closesemantics = new Setting<CloseSemantics>(CloseSemantics.LUA54);
                    upvaluedeclarationtype = new Setting<UpvalueDeclarationType>(UpvalueDeclarationType.HEADER);
                    fortarget = new Setting<Op>(null);
                    tfortarget = new Setting<Op>(null);
                    whileformat = new Setting<WhileFormat>(WhileFormat.TOP_CONDITION);
                    allowpreceedingsemicolon = new Setting<bool>(true);
                    usenestinglongstrings = new Setting<bool>(false);
                    environmenttable = new Setting<string>("_ENV");
                    useifbreakrewrite = new Setting<bool>(true);
                    useifgotorewrite = new Setting<Maybe>(Maybe.MAYBE);
                    usegoto = new Setting<bool>(true);
                    usedeadclose = new Setting<bool>(false);
                    rkoffset = new Setting<int?>(null);
                    allownegativeint = new Setting<bool>(true);
                    constantslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    functionslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    locallengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    upvaluelengthmode = new Setting<ListLengthMode>(ListLengthMode.IGNORE);
                    useglobaldecl = new Setting<bool>(false);
                    break;
                case 5:
                    varargtype = new Setting<VarArgType>(VarArgType.NAMED);
                    useupvaluecountinheader = new Setting<bool>(true);
                    headertype = HeaderType.LUA55;
                    stringtype = StringType.LUA55;
                    upvaluetype = UpvalueType.LUA54;
                    functiontype = FunctionType.LUA55;
                    typemap = TypeMapType.LUA54;
                    opcodemap = OpcodeMapType.LUA55;
                    _defaultop = Op.DEFAULT54;
                    instructionformat = new Setting<InstructionFormat>(InstructionFormat.LUA54);
                    outerblockscopeadjustment = new Setting<int?>(0);
                    extendedrepeatscope = new Setting<bool>(false);
                    closesemantics = new Setting<CloseSemantics>(CloseSemantics.LUA54);
                    upvaluedeclarationtype = new Setting<UpvalueDeclarationType>(UpvalueDeclarationType.HEADER);
                    fortarget = new Setting<Op>(null);
                    tfortarget = new Setting<Op>(null);
                    whileformat = new Setting<WhileFormat>(WhileFormat.TOP_CONDITION);
                    allowpreceedingsemicolon = new Setting<bool>(true);
                    usenestinglongstrings = new Setting<bool>(false);
                    environmenttable = new Setting<string>("_ENV");
                    useifbreakrewrite = new Setting<bool>(true);
                    useifgotorewrite = new Setting<Maybe>(Maybe.NO);
                    usegoto = new Setting<bool>(true);
                    usedeadclose = new Setting<bool>(true);
                    rkoffset = new Setting<int?>(null);
                    allownegativeint = new Setting<bool>(true);
                    constantslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    functionslengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    locallengthmode = new Setting<ListLengthMode>(ListLengthMode.STRICT);
                    upvaluelengthmode = new Setting<ListLengthMode>(ListLengthMode.IGNORE);
                    useglobaldecl = new Setting<bool>(true);
                    break;
                default: throw new InvalidOperationException();
            }
        }
        else
        {
            throw new InvalidOperationException();
        }

        _reservedWords = new HashSet<string>
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function",
            "if", "in", "local", "nil", "not", "or", "repeat", "return", "then",
            "true", "until", "while",
        };
        if (usegoto.Get())
        {
            _reservedWords.Add("goto");
        }
        if (useglobaldecl.Get())
        {
            _reservedWords.Add("global");
        }

        _lheadertype = LHeaderType.Get(headertype);
        _lstringtype = LStringType.Get(stringtype);
        _lconstanttype = new LConstantType();
        _lupvaluetype = LUpvalueType.Get(upvaluetype);
        _lfunctiontype = LFunctionType.Get(functiontype);
        _typemap = new TypeMap(typemap);
        _opcodemap = new OpcodeMap(opcodemap);
    }

    public int GetVersionMajor() => _major;
    public int GetVersionMinor() => _minor;
    public string GetName() => _name;

    public bool HasGlobalSupport() => environmenttable.Get() == null;

    public bool IsEnvironmentTable(string name)
    {
        string env = environmenttable.Get();
        return env != null && name == env;
    }

    public bool IsReserved(string name) => _reservedWords.Contains(name);

    public LHeaderType GetLHeaderType() => _lheadertype;
    public LStringType GetLStringType() => _lstringtype;
    public LConstantType GetLConstantType() => _lconstanttype;
    public LUpvalueType GetLUpvalueType() => _lupvaluetype;
    public LFunctionType GetLFunctionType() => _lfunctiontype;
    public TypeMap GetTypeMap() => _typemap;
    public OpcodeMap GetOpcodeMap() => _opcodemap;
    public Op GetDefaultOp() => _defaultop;
}
