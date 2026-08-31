namespace Parser;

public record Statement { }

public record Group(Statement[] Content) : Statement
{
  public override string ToString() => $"[{string.Join(", ", Content.Select(s => s.ToString()))}]";
}
public record Scope(Statement[] Content) : Statement
{
  public override string ToString() => $"[{string.Join(", ", Content.Select(s => s.ToString()))}]";
}
public record FunctionDecl(Function Func) : Statement
{
  public override string ToString() => $"fun {Func.Modifiers} {Func.Name}({string.Join(", ", Func.Arguments.Select(a => a.ToString()))}) : {Func.ReturnType} -> {Func.Body}";
}

public record Return(Expression? expr) : Statement
{
  readonly string e = expr != null ? $"{expr}" : "";
  public override string ToString() => $"return {e}".Trim();
}

public record StructDecl(Composite Struct) : Statement
{
  public override string ToString() => $"{Struct}";
}

public record UnionDecl(Composite Union) : Statement
{
  public override string ToString() => $"{Union}";
}

public record VariableDecl(Variable Variable, Expression? Expression) : Statement
{
  public override string ToString() => $"var {Variable} = {Expression}";
}

public record TypeDefinition(string Name, DataType Type) : Statement
{
  public override string ToString() => $"type {Name} = {Type}";
}

public record IgnoredExpr(Expression Expression) : Statement
{
  public override string ToString() => $"{Expression}";
}

public record IfStatement(Expression Expression, Statement Statement, Statement? Else) : Statement
{
  public override string ToString()
  {
    string s = Else != null ? $" else {Else}" : "";
    return $"if ({Expression}) {Statement}{s}";
  }
}

public record WhileStmt(Expression Expression, Statement Body) : Statement
{
  public override string ToString() => $"while ({Expression}) {Body}";
}

public record DoWhileStmt(Expression Expression, Statement Body) : Statement
{
  public override string ToString() => $"do {Body} while ({Expression});";
}

public record ForStmt(Statement Init, Expression Condition, Statement Update, Statement Body) : Statement
{
  public override string ToString() => $"for ({Init}; {Condition}; {Update}) {Body}";
}

public record BreakStmt() : Statement
{
  public override string ToString() => $"break";
}

public record ContinueStmt() : Statement
{
  public override string ToString() => $"continue";
}

public record SwitchStmt(Expression Expression, (Expression, Statement)[] Cases, Statement? Default) : Statement
{
  public override string ToString() => $"switch ({Expression}) {{{Cases}\n{Default}}}";
}

public record Nop() : Statement
{
  public override string ToString() => $"nop";
};
