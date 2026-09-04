using Lexer;

namespace Parser;

public record Statement
{
  public virtual TokenInfo GetInfo() => new(0, "");
}

public record Group(TokenInfo Info, Statement[] Content) : Statement
{
  public override string ToString() => $"[{string.Join(", ", Content.Select(s => s.ToString()))}]";
  public override TokenInfo GetInfo() => Info;
}
public record Scope(TokenInfo Info, Statement[] Content) : Statement
{
  public override string ToString() => $"[{string.Join(", ", Content.Select(s => s.ToString()))}]";
  public override TokenInfo GetInfo() => Info;

}
public record FunctionDecl(TokenInfo Info, Function Func) : Statement
{
  public override string ToString() => $"fun {Func.Modifiers} {Func.Name}({string.Join(", ", Func.Arguments.Select(a => a.ToString()))}) : {Func.ReturnType} -> {Func.Body}";
  public override TokenInfo GetInfo() => Info;

}

public record Return(TokenInfo Info, Expression? expr) : Statement
{
  readonly string e = expr != null ? $"{expr}" : "";
  public override string ToString() => $"return {e}".Trim();
  public override TokenInfo GetInfo() => Info;

}

public record StructDecl(TokenInfo Info, Composite Struct) : Statement
{
  public override string ToString() => $"{Struct}";
  public override TokenInfo GetInfo() => Info;

}

public record UnionDecl(TokenInfo Info, Composite Union) : Statement
{
  public override string ToString() => $"{Union}";
  public override TokenInfo GetInfo() => Info;

}

public record VariableDecl(TokenInfo Info, Variable Variable, Expression? Expression) : Statement
{
  public override string ToString() => $"var {Variable} = {Expression}";
  public override TokenInfo GetInfo() => Info;

}

public record TypeDefinition(TokenInfo Info, string Name, DataType Type) : Statement
{
  public override string ToString() => $"type {Name} = {Type}";
  public override TokenInfo GetInfo() => Info;

}

public record IgnoredExpr(Expression Expression) : Statement
{
  public override string ToString() => $"{Expression}";
}

public record IfStatement(TokenInfo Info, Expression Expression, Statement Statement, Statement? Else) : Statement
{
  public override string ToString()
  {
    string s = Else != null ? $" else {Else}" : "";
    return $"if ({Expression}) {Statement}{s}";
  }
  public override TokenInfo GetInfo() => Info;

}

public record WhileStmt(TokenInfo Info, Expression Expression, Statement Body) : Statement
{
  public override string ToString() => $"while ({Expression}) {Body}";
  public override TokenInfo GetInfo() => Info;

}

public record DoWhileStmt(TokenInfo Info, Expression Expression, Statement Body) : Statement
{
  public override string ToString() => $"do {Body} while ({Expression});";
  public override TokenInfo GetInfo() => Info;

}

public record ForStmt(TokenInfo Info, Statement Init, Expression Condition, Statement Update, Statement Body) : Statement
{
  public override string ToString() => $"for ({Init}; {Condition}; {Update}) {Body}";
  public override TokenInfo GetInfo() => Info;

}

public record BreakStmt(TokenInfo Info) : Statement
{
  public override string ToString() => $"break";
  public override TokenInfo GetInfo() => Info;

}

public record ContinueStmt(TokenInfo Info) : Statement
{
  public override string ToString() => $"continue";
  public override TokenInfo GetInfo() => Info;

}

public record SwitchStmt(TokenInfo Info, Expression Expression, (Expression, Statement)[] Cases, Statement? Default) : Statement
{
  public override string ToString() => $"switch ({Expression}) {{{Cases}\n{Default}}}";
  public override TokenInfo GetInfo() => Info;

}

public record Nop() : Statement
{
  public override string ToString() => $"nop";
};

public record ExternVariable(TokenInfo Info, Variable Variable) : Statement
{
  public override string ToString() => $"extern var {Variable};";
  public override TokenInfo GetInfo() => Info;

}

public record ExternFunction(TokenInfo Info, Function Function) : Statement
{
  public override string ToString() => $"extern fun {Function};";
  public override TokenInfo GetInfo() => Info;

}
