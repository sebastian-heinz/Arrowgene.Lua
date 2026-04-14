using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.VariableFinder. Synthesises a
/// best-effort <see cref="Declaration"/> list for bytecode that
/// was compiled without debug information, by abstractly
/// interpreting the instruction stream and classifying each
/// register's usage at every line as local, temporary, or
/// unknown. Registers that appear live across multiple
/// read/write boundaries, or that outlive a temporary slot,
/// are promoted to locals.
/// </summary>
public static class VariableFinder
{
    private class RegisterState
    {
        public RegisterState()
        {
            last_written = 1;
            last_read = -1;
            read_count = 0;
            temporary = false;
            local = false;
            read = false;
            written = false;
        }

        public int last_written;
        public int last_read;
        public int read_count;
        public bool temporary;
        public bool local;
        public bool read;
        public bool written;
    }

    private class RegisterStates
    {
        private readonly int registers;
        private readonly int lines;
        private readonly RegisterState[][] states;

        public RegisterStates(int registers, int lines)
        {
            this.registers = registers;
            this.lines = lines;
            states = new RegisterState[lines][];
            for (int line = 0; line < lines; line++)
            {
                states[line] = new RegisterState[registers];
                for (int register = 0; register < registers; register++)
                {
                    states[line][register] = new RegisterState();
                }
            }
        }

        public RegisterState Get(int register, int line)
        {
            return states[line - 1][register];
        }

        public void SetWritten(int register, int line)
        {
            Get(register, line).written = true;
            Get(register, line + 1).last_written = line;
        }

        public void SetRead(int register, int line)
        {
            Get(register, line).read = true;
            Get(register, Get(register, line).last_written).read_count++;
            Get(register, Get(register, line).last_written).last_read = line;
        }

        public void SetLocalRead(int register, int line)
        {
            for (int r = 0; r <= register; r++)
            {
                Get(r, Get(r, line).last_written).local = true;
            }
        }

        public void SetLocalWrite(int register_min, int register_max, int line)
        {
            for (int r = 0; r < register_min; r++)
            {
                Get(r, Get(r, line).last_written).local = true;
            }
            for (int r = register_min; r <= register_max; r++)
            {
                Get(r, line).local = true;
            }
        }

        public void SetTemporaryRead(int register, int line)
        {
            for (int r = register; r < registers; r++)
            {
                Get(r, Get(r, line).last_written).temporary = true;
            }
        }

        public void SetTemporaryWrite(int register_min, int register_max, int line)
        {
            for (int r = register_max + 1; r < registers; r++)
            {
                Get(r, Get(r, line).last_written).temporary = true;
            }
            for (int r = register_min; r <= register_max; r++)
            {
                Get(r, line).temporary = true;
            }
        }

        public void NextLine(int line)
        {
            if (line + 1 < lines)
            {
                for (int r = 0; r < registers; r++)
                {
                    if (Get(r, line).last_written > Get(r, line + 1).last_written)
                    {
                        Get(r, line + 1).last_written = Get(r, line).last_written;
                    }
                }
            }
        }
    }

    private static bool IsConstantReference(Decompiler d, int value)
    {
        return d.function.header.extractor.IsK(value);
    }

    private static int lc = 0;

    public static Declaration[] Process(Decompiler d, int args, int registers)
    {
        Code code = d.code;
        RegisterStates states = new RegisterStates(registers, code.Length());
        bool[] skip = new bool[code.Length()];
        for (int line = 1; line <= code.Length(); line++)
        {
            states.NextLine(line);
            if (skip[line - 1]) continue;
            int A = code.A(line);
            int B = code.B(line);
            int C = code.C(line);
            switch (code.Op(line).Type)
            {
                case OpT.MOVE:
                    states.SetWritten(A, line);
                    states.SetRead(B, line);
                    if (A < B)
                    {
                        states.SetLocalWrite(A, A, line);
                    }
                    else if (B < A)
                    {
                        states.SetLocalRead(B, line);
                    }
                    break;
                case OpT.LOADK:
                case OpT.LOADBOOL:
                case OpT.GETUPVAL:
                case OpT.GETGLOBAL:
                case OpT.NEWTABLE:
                case OpT.NEWTABLE50:
                    states.SetWritten(A, line);
                    break;
                case OpT.LOADNIL:
                {
                    int maximum = B;
                    int register = code.A(line);
                    while (register <= maximum)
                    {
                        states.SetWritten(register, line);
                        register++;
                    }
                    break;
                }
                case OpT.LOADNIL52:
                {
                    int maximum = A + B;
                    int register = code.A(line);
                    while (register <= maximum)
                    {
                        states.SetWritten(register, line);
                        register++;
                    }
                    break;
                }
                case OpT.GETTABLE:
                    states.SetWritten(A, line);
                    if (!IsConstantReference(d, code.B(line))) states.SetRead(B, line);
                    if (!IsConstantReference(d, code.C(line))) states.SetRead(C, line);
                    break;
                case OpT.SETGLOBAL:
                case OpT.SETUPVAL:
                    states.SetRead(A, line);
                    break;
                case OpT.SETTABLE:
                case OpT.ADD:
                case OpT.SUB:
                case OpT.MUL:
                case OpT.DIV:
                case OpT.MOD:
                case OpT.POW:
                    states.SetWritten(A, line);
                    if (!IsConstantReference(d, code.B(line))) states.SetRead(B, line);
                    if (!IsConstantReference(d, code.C(line))) states.SetRead(C, line);
                    break;
                case OpT.SELF:
                    states.SetWritten(A, line);
                    states.SetWritten(A + 1, line);
                    states.SetRead(B, line);
                    if (!IsConstantReference(d, code.C(line))) states.SetRead(C, line);
                    break;
                case OpT.UNM:
                case OpT.NOT:
                case OpT.LEN:
                    states.Get(code.A(line), line).written = true;
                    states.Get(code.B(line), line).read = true;
                    break;
                case OpT.CONCAT:
                    states.SetWritten(A, line);
                    for (int register = B; register <= C; register++)
                    {
                        states.SetRead(register, line);
                        states.SetTemporaryRead(register, line);
                    }
                    break;
                case OpT.SETLIST:
                    states.SetTemporaryRead(A + 1, line);
                    break;
                case OpT.JMP:
                case OpT.JMP52:
                    break;
                case OpT.EQ:
                case OpT.LT:
                case OpT.LE:
                    if (!IsConstantReference(d, code.B(line))) states.SetRead(B, line);
                    if (!IsConstantReference(d, code.C(line))) states.SetRead(C, line);
                    break;
                case OpT.TEST:
                    states.SetRead(A, line);
                    break;
                case OpT.TESTSET:
                    states.SetWritten(A, line);
                    states.SetLocalWrite(A, A, line);
                    states.SetRead(B, line);
                    break;
                case OpT.CLOSURE:
                {
                    LFunction f = d.function.functions[code.Bx(line)];
                    foreach (LUpvalue upvalue in f.upvalues)
                    {
                        if (upvalue.instack)
                        {
                            states.SetLocalRead(upvalue.idx, line);
                        }
                    }
                    states.Get(code.A(line), line).written = true;
                    break;
                }
                case OpT.CALL:
                case OpT.TAILCALL:
                {
                    if (code.Op(line).Type != OpT.TAILCALL)
                    {
                        if (C >= 2)
                        {
                            for (int register = A; register <= A + C - 2; register++)
                            {
                                states.SetWritten(register, line);
                            }
                        }
                    }
                    for (int register = A; register <= A + B - 1; register++)
                    {
                        states.SetRead(register, line);
                        states.SetTemporaryRead(register, line);
                    }
                    if (C >= 2)
                    {
                        int nline = line + 1;
                        int register = A + C - 2;
                        while (register >= A && nline <= code.Length())
                        {
                            if (code.Op(nline).Type == OpT.MOVE && code.B(nline) == register)
                            {
                                states.SetWritten(code.A(nline), nline);
                                states.SetRead(code.B(nline), nline);
                                states.SetLocalWrite(code.A(nline), code.A(nline), nline);
                                skip[nline - 1] = true;
                            }
                            register--;
                            nline++;
                        }
                    }
                    break;
                }
                case OpT.RETURN:
                {
                    if (B == 0) B = registers - code.A(line) + 1;
                    for (int register = A; register <= A + B - 2; register++)
                    {
                        states.Get(register, line).read = true;
                    }
                    break;
                }
                default:
                    break;
            }
        }
        for (int register = 0; register < registers; register++)
        {
            states.SetWritten(register, 1);
        }
        for (int line = 1; line <= code.Length(); line++)
        {
            for (int register = 0; register < registers; register++)
            {
                RegisterState s = states.Get(register, line);
                if (s.written)
                {
                    if (s.read_count >= 2 || (line >= 2 && s.read_count == 0))
                    {
                        states.SetLocalWrite(register, register, line);
                    }
                }
            }
        }
        for (int line = 1; line <= code.Length(); line++)
        {
            for (int register = 0; register < registers; register++)
            {
                RegisterState s = states.Get(register, line);
                if (s.written && s.temporary)
                {
                    List<int> ancestors = new List<int>();
                    for (int read = 0; read < registers; read++)
                    {
                        RegisterState rr = states.Get(read, line);
                        if (rr.read && !rr.local)
                        {
                            ancestors.Add(read);
                        }
                    }
                    int pline;
                    for (pline = line - 1; pline >= 1; pline--)
                    {
                        bool any_written = false;
                        for (int pregister = 0; pregister < registers; pregister++)
                        {
                            if (states.Get(pregister, pline).written && ancestors.Contains(pregister))
                            {
                                any_written = true;
                                ancestors.Remove(pregister);
                            }
                        }
                        if (!any_written)
                        {
                            break;
                        }
                        for (int pregister = 0; pregister < registers; pregister++)
                        {
                            RegisterState a = states.Get(pregister, pline);
                            if (a.read && !a.local)
                            {
                                ancestors.Add(pregister);
                            }
                        }
                    }
                    foreach (int ancestor in ancestors)
                    {
                        if (pline >= 1)
                        {
                            states.SetLocalRead(ancestor, pline);
                        }
                    }
                }
            }
        }
        List<Declaration> declList = new List<Declaration>(registers);
        for (int register = 0; register < registers; register++)
        {
            string id = "L";
            bool local = false;
            bool temporary = false;
            int read = 0;
            int written = 0;
            int start = 0;
            if (register < args)
            {
                local = true;
                id = "A";
            }
            bool is_arg = false;
            if (register == args)
            {
                switch (d.GetVersion().varargtype.Get())
                {
                    case Version.VarArgType.ARG:
                    case Version.VarArgType.HYBRID:
                        if ((d.function.vararg & 1) != 0)
                        {
                            local = true;
                            is_arg = true;
                        }
                        break;
                    case Version.VarArgType.ELLIPSIS:
                        break;
                }
            }
            if (!local && !temporary)
            {
                for (int line = 1; line <= code.Length(); line++)
                {
                    RegisterState state = states.Get(register, line);
                    if (state.local)
                    {
                        temporary = false;
                        local = true;
                    }
                    if (state.temporary)
                    {
                        start = line + 1;
                        temporary = true;
                    }
                    if (state.read)
                    {
                        written = 0; read++;
                    }
                    if (state.written)
                    {
                        if (written > 0 && read == 0)
                        {
                            temporary = false;
                            local = true;
                        }
                        read = 0; written++;
                    }
                }
            }
            if (!local && !temporary)
            {
                if (read >= 2 || (read == 0 && written != 0))
                {
                    local = true;
                }
            }
            if (local)
            {
                string name;
                if (is_arg)
                {
                    name = "arg";
                }
                else
                {
                    name = id + register + "_" + lc++;
                }
                Declaration decl = new Declaration(name, start, code.Length() + d.GetVersion().outerblockscopeadjustment.Get().Value);
                decl.register = register;
                declList.Add(decl);
            }
        }
        return declList.ToArray();
    }
}
