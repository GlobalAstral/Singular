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
