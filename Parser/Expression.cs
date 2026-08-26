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

  public DataType GetReturnType() => References.GetArrayType(type, new LiteralExpr(new IntLiteral(Expressions.Length)));
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

public class UnaryExpression(Expression expr, UnaryExpression.UnaryOperator op) : Expression
{
  public enum UnaryOperator
  {
    Minus, Not, BitNot, PreInc, PreDec, Deref, Ref, Sizeof
  }

  public Expression Base {get;} = expr;
  public UnaryOperator Operator {get;} = op;

  private DataType Deref()
  {
    DataType temp = Base.GetReturnType();
    if (!temp.Matches<PointerType>())
      throw new Exception("Cannot dereference a non-pointer type");
    PointerType type = (PointerType)temp;
    return type.Target;
  }

  private DataType Numeric()
  {
    DataType temp = Base.GetReturnType();
    if (!DataType.IsNumeric(temp))
      throw new Exception("Cannot use a numeric operator on a non-numeric type");
    return temp;
  }

  private DataType Signed()
  {
    DataType temp = Base.GetReturnType();
    if (DataType.IsUnsigned(temp))
      throw new Exception("Cannot use a signed numeric operator on a non-signed numeric type");
    return temp;
  }

  public DataType GetReturnType() => Operator switch
  {
    UnaryOperator.Minus => Signed(),
    UnaryOperator.Not => BooleanType.INSTANCE,
    UnaryOperator.BitNot => Numeric(),
    UnaryOperator.PreInc => Numeric(),
    UnaryOperator.PreDec => Numeric(),
    UnaryOperator.Deref => Deref(),
    UnaryOperator.Ref => References.GetPointerType(Base.GetReturnType()),
    UnaryOperator.Sizeof => ULongType.INSTANCE,

    _ => throw new ArgumentOutOfRangeException(nameof(Operator)),
  };
}

public class MemberAccess(Expression expression, Variable field) : Expression
{
  public Expression Expression {get;} = expression;
  public Variable Field {get;} = field;
  public DataType GetReturnType() => Field.Type;
  public override string ToString() => $"{Expression}.{Field.Name}";
}

public class IndexExpr(Expression @base, Expression index, DataType ReturnType) : Expression
{
  public Expression Base {get;} = @base;
  public Expression Index {get;} = index;

  public DataType GetReturnType() => ReturnType;
}

public class FunctionCall(Expression @base, Expression[] args, DataType ReturnType) : Expression
{
  public Expression Base {get;} = @base;
  public Expression[] Args {get;} = args;

  public DataType GetReturnType() => ReturnType;
}

public class Cast(Expression @base, DataType type) : Expression
{
  public Expression Base {get;} = @base;
  public DataType Type {get;} = type;

  public DataType GetReturnType() => Type;
}

public class BitCast(Expression @base, DataType type) : Expression
{
  public Expression Base {get;} = @base;
  public DataType Type {get;} = type;

  public DataType GetReturnType() => Type;
}

public class TernaryOperator(Expression condition, Expression result, Expression fail) : Expression
{
  public Expression Condition {get;} = condition;
  public Expression Success {get;} = result;
  public Expression Fail {get;} = fail;

  public DataType GetReturnType() => Success.GetReturnType();
}
