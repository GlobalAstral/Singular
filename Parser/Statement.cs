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

