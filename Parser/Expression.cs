
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
}

public class CompositeLiteral(DataType type, Dictionary<string, Expression> expressions) : Expression
{
  public DataType Type {get;} = type;
  public Dictionary<string, Expression> Expressions {get;} = expressions;
  public DataType GetReturnType() => Type;
}
