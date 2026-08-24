using System.Text;

namespace Parser;

public interface Expression
{
  public DataType GetReturnType();
}

public class LiteralExpr(Literal Lit) : Expression
{
  public Literal Lit {get;} = Lit;

  public DataType GetReturnType() => Lit.GetReturnType();
  public override string ToString() => $"{Lit}";
}

public class RawExpr(DataType returnType, string generated) : Expression
{
  public string Generated {get;} = generated;

  public DataType GetReturnType() => returnType;
  public override string ToString() => $"Raw{{\"{Generated}\"}} : {returnType}";
}

public class IdentifierExpression(Variable Variable) : Expression
{
  public Variable Variable {get;} = Variable;
  public DataType GetReturnType() => Variable.Type;
  public override string ToString() => $"{Variable}";
}

public class ArrayLiteral(DataType type, Expression[] Expressions) : Expression
{
  public Expression[] Expressions {get;} = Expressions;

  public DataType GetReturnType() => References.GetArrayType(type);
  public override string ToString() => $"[{string.Join(", ", Expressions)}]";
}

public class CompositeLiteral(DataType type, Dictionary<string, Expression> expressions) : Expression
{
  public DataType Type {get;} = type;
  public Dictionary<string, Expression> Expressions {get;} = expressions;
  public DataType GetReturnType() => Type;
  public override string ToString()
  {
    CompositeType composite = (CompositeType) Type;
    StringBuilder builder = new();
    builder.Append($"{composite.Comp.Kind} {composite.Comp.Name} {{");
    int count = 0;
    foreach (var item in composite.Comp.Fields)
    {
      if (count > 0)
        builder.Append(", ");
      builder.Append($"{item} = {Expressions.GetValueOrDefault(item.Name, item.Type.GetNull())}");
      count++;
    }
    foreach (var item in composite.Comp.Statics)
    {
      Variable var = item.Key;
      Expression? expression = item.Value;
      if (count > 0)
        builder.Append(", ");
      builder.Append($"{var} = {expression ?? var.Type.GetNull()}");
      count++;
    }
    builder.Append('}');
    return builder.ToString();
  }
}

public class FunctionPointer(Function func) : Expression
{
  public Function Function {get;} = func;
  public DataType GetReturnType() => References.GetFunctionType(Function.ReturnType, [.. Function.Arguments.Select(a => a.Type)]);
  public override string ToString() => $"{Function}";
}

public class Lambda(Variable[] arguments, DataType? retType, Statement body) : Expression
{
  public Variable[] Arguments {get;} = arguments;
  public DataType? ReturnType {get;} = retType;
  public Statement Body {get;} = body;
  public DataType GetReturnType() => References.GetFunctionType(ReturnType, [ .. Arguments.Select(a => a.Type) ]);
  public override string ToString() => $"({string.Join(", ", Arguments)}) : {ReturnType} {Body}";
}
