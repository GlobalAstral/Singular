namespace Parser;

public interface Context { }

public class FunctionContext(DataType? retType, Variable[] arguments) : Context
{
  public DataType? ReturnType {get;} = retType;
  public Variable[] Arguments {get;} = arguments;
}

public class CompositeContext(Composite Comp) : Context
{
  public Composite Comp {get;} = Comp;
}

public class ScopeContext(FunctionContext? f = null) : Context
{
  public FunctionContext? FunctionContext {get;} = f;
  public List<Variable> Locals {get;} = [];
  public Stack<Statement> Defers {get;} = [];

  public Statement ResolveDefers(Statement? appended = null)
  {
    List<Statement> statements = [];
    while (Defers.Count > 0)
      statements.Add(Defers.Pop());
    if (appended != null)
      statements.Add(appended);
    return statements.Count != 1 ? new Group([.. statements]) : statements.First();
  }
}

public class LoopContext() : Context
{
  
}
