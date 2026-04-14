using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Targets;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Registers. Tracks, per (register, line), the
/// active <see cref="Declaration"/> in that slot, the most recent
/// expression value written there, and the pc on which that value was
/// updated. Owns the per-line propagation pass (<see cref="StartLine"/>)
/// that copies the previous line's state forward, plus the for-loop
/// declaration helpers used by the block resolver.
/// </summary>
public sealed class Registers
{
    public readonly int registers;
    public readonly int length;

    private readonly Declaration[][] decls;
    private readonly Function f;
    public readonly bool isNoDebug;
    private readonly Expression[][] values;
    private readonly int[][] updated;
    private readonly bool[] startedLines;

    public Registers(int registers, int length, Declaration[] declList, Function f, bool isNoDebug)
    {
        this.registers = registers;
        this.length = length;
        decls = new Declaration[registers][];
        for (int i = 0; i < registers; i++)
        {
            decls[i] = new Declaration[length + 1];
        }
        for (int i = 0; i < declList.Length; i++)
        {
            Declaration decl = declList[i];
            int register = 0;
            while (decls[register][decl.begin] != null)
            {
                register++;
            }
            decl.register = register;
            for (int line = decl.begin; line <= decl.end; line++)
            {
                decls[register][line] = decl;
            }
        }
        values = new Expression[registers][];
        for (int i = 0; i < registers; i++)
        {
            values[i] = new Expression[length + 1];
        }
        Expression nil = ConstantExpression.CreateNil(0);
        for (int register = 0; register < registers; register++)
        {
            values[register][0] = nil;
        }
        updated = new int[registers][];
        for (int i = 0; i < registers; i++)
        {
            updated[i] = new int[length + 1];
        }
        startedLines = new bool[length + 1];
        this.f = f;
        this.isNoDebug = isNoDebug;
    }

    public Function GetFunction() => f;

    public bool IsAssignable(int register, int line)
    {
        return IsLocal(register, line) &&
               !decls[register][line].IsInternalName() &&
               (!decls[register][line].forLoop || isNoDebug);
    }

    public bool IsLocal(int register, int line)
    {
        if (register < 0) return false;
        return decls[register][line] != null;
    }

    public bool IsLocalName(string name, int line)
    {
        for (int register = 0; register < registers; register++)
        {
            Declaration decl = decls[register][line];
            if (decl == null) break;
            if (decl.name == name) return true;
        }
        return false;
    }

    public bool IsNewLocal(int register, int line)
    {
        Declaration decl = decls[register][line];
        return decl != null &&
               decl.begin == line &&
               !decl.IsInternalName() &&
               !decl.forLoop &&
               !decl.forLoopExplicit &&
               !decl.namedVararg;
    }

    public IList<Declaration> GetNewLocals(int line)
    {
        return GetNewLocals(line, 0);
    }

    public IList<Declaration> GetNewLocals(int line, int first)
    {
        if (first < 0) first = 0;
        var locals = new List<Declaration>(registers - first > 0 ? registers - first : 0);
        for (int register = first; register < registers; register++)
        {
            if (IsNewLocal(register, line))
            {
                locals.Add(GetDeclaration(register, line));
            }
        }
        return locals;
    }

    public Declaration GetDeclaration(int register, int line)
    {
        return decls[register][line];
    }

    public void StartLine(int line)
    {
        startedLines[line] = true;
        for (int register = 0; register < registers; register++)
        {
            values[register][line] = values[register][line - 1];
            updated[register][line] = updated[register][line - 1];
        }
    }

    public bool IsKConstant(int register) => f.IsConstant(register);

    public Expression GetExpression(int register, int line)
    {
        if (IsLocal(register, line - 1))
        {
            return new LocalVariable(GetDeclaration(register, line - 1));
        }
        return values[register][line - 1];
    }

    public Expression GetKExpression(int register, int line)
    {
        if (f.IsConstant(register))
        {
            return f.GetConstantExpression(f.ConstantIndex(register));
        }
        return GetExpression(register, line);
    }

    public Expression GetKExpression54(int register, bool k, int line)
    {
        if (k)
        {
            return f.GetConstantExpression(register);
        }
        return GetExpression(register, line);
    }

    public Expression GetValue(int register, int line)
    {
        if (isNoDebug)
        {
            return GetExpression(register, line);
        }
        return values[register][line - 1];
    }

    public int GetUpdated(int register, int line) => updated[register][line];

    public void SetValue(int register, int line, Expression expression)
    {
        values[register][line] = expression;
        updated[register][line] = line;
    }

    public Target GetTarget(int register, int line)
    {
        if (!isNoDebug && !IsLocal(register, line))
        {
            throw new System.InvalidOperationException("No declaration exists in register " + register + " at line " + line);
        }
        return new VariableTarget(decls[register][line]);
    }

    public void SetInternalLoopVariable(int register, int begin, int end)
    {
        Declaration decl = GetDeclaration(register, begin);
        if (decl == null)
        {
            decl = new Declaration("_FOR_", begin, end);
            decl.register = register;
            NewDeclaration(decl, register, begin, end);
            if (!isNoDebug)
            {
                throw new System.InvalidOperationException("TEMP");
            }
        }
        else if (isNoDebug)
        {
            //
        }
        else
        {
            if (decl.begin != begin || decl.end != end)
            {
                System.Console.Error.WriteLine("given: " + begin + " " + end);
                System.Console.Error.WriteLine("expected: " + decl.begin + " " + decl.end);
                throw new System.InvalidOperationException();
            }
        }
        decl.forLoop = true;
    }

    public void SetExplicitLoopVariable(int register, int begin, int end)
    {
        Declaration decl = GetDeclaration(register, begin);
        if (decl == null)
        {
            decl = new Declaration("_FORV_" + register + "_", begin, end);
            decl.register = register;
            NewDeclaration(decl, register, begin, end);
            if (!isNoDebug)
            {
                throw new System.InvalidOperationException("TEMP");
            }
        }
        else if (isNoDebug)
        {
        }
        else
        {
            if (decl.begin != begin || decl.end != end)
            {
                System.Console.Error.WriteLine("given: " + begin + " " + end);
                System.Console.Error.WriteLine("expected: " + decl.begin + " " + decl.end);
                throw new System.InvalidOperationException();
            }
        }
        decl.forLoopExplicit = true;
    }

    private void NewDeclaration(Declaration decl, int register, int begin, int end)
    {
        for (int line = begin; line <= end; line++)
        {
            decls[register][line] = decl;
        }
    }

    public Version GetVersion() => f.GetVersion();
}
