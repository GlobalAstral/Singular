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

public class UnaryExpression : Expression
{
  public UnaryExpression(Expression expr, UnaryOperator op)
  {
    Base = expr;
    Operator = op;
    GetReturnType();
  }
  public enum UnaryOperator
  {
    Minus, Not, BitNot, PreInc, PreDec, Deref, Ref, Sizeof
  }

  public Expression Base {get;}
  public UnaryOperator Operator {get;}

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

public class PostIncrement(Expression @base, int direction) : Expression
{
  public Expression Base {get;} = @base;
  public int Direction {get;} = direction;

  public DataType GetReturnType() => Base.GetReturnType();
}

public class BinaryExpr : Expression
{
  public static uint Precedence(BinaryOp op) => op switch {
    BinaryOp.Mul
    or BinaryOp.Div
    or BinaryOp.Mod
      => 11,

    BinaryOp.Add
    or BinaryOp.Sub
      => 10,

    BinaryOp.Shl
    or BinaryOp.Shr
      => 9,

    BinaryOp.Greater
    or BinaryOp.Less
    or BinaryOp.GreaterEqual
    or BinaryOp.LessEqual
      => 8,

    BinaryOp.Equals
    or BinaryOp.NotEquals
      => 7,

    BinaryOp.BitAnd => 6,
    BinaryOp.BitXor => 5,
    BinaryOp.BitOr => 4,
    BinaryOp.And => 3,
    BinaryOp.Or => 2,
    BinaryOp.Assign => 1,

    _ => throw new ArgumentOutOfRangeException(nameof(op))
  };

  public BinaryExpr(Expression left, Expression right, BinaryExpr.BinaryOp op)
  {
    Left = left;
    Right = right;
    Operator = op;
    GetReturnType();
  }
  public Expression Left {get;}
  public Expression Right {get;}
  public BinaryOp Operator {get;}
  private DataType Arith()
  {
    DataType LeftType = Left.GetReturnType();
    DataType RightType = Right.GetReturnType();
    if ((!DataType.IsNumeric(LeftType) && !LeftType.Matches<PointerType>()) || (!DataType.IsNumeric(RightType) && !RightType.Matches<PointerType>()))
      throw new Exception("Cannot do arithmetics with non-numeric types and non-pointer types");
    if (!LeftType.CanAccept(RightType))
      throw new Exception("Cannot do arithmetics with non-compatible types");
    return LeftType;
  }

  private DataType Modulus()
  {
    DataType LeftType = Left.GetReturnType();
    DataType RightType = Right.GetReturnType();

    if (!DataType.IsNumeric(LeftType) || !DataType.IsNumeric(RightType))
      throw new Exception("Cannot do modulus with non-numeric types");
    if (LeftType.Matches<FloatType>() || LeftType.Matches<DoubleType>() || RightType.Matches<FloatType>() || RightType.Matches<DoubleType>())
      throw new Exception("Cannot do modulus with floating-point types");
    if (!LeftType.CanAccept(RightType))
      throw new Exception("Cannot do modulus with incompatible types");
    
    return LeftType;
  }

  private DataType Assign()
  {
    if (Left is not IdentifierExpression && Left is not IndexExpr && Left is not MemberAccess && !(Left is UnaryExpression u
      && u.Operator == UnaryExpression.UnaryOperator.Deref))
      throw new Exception($"{Left} is not a modifiable lvalue");

    if (Left is IdentifierExpression ident && !ident.Variable.Modifiers.IsMutable)
      throw new Exception($"{ident.Variable} is a constant");
    
    if (Left is MemberAccess memberAccess && !memberAccess.Field.Modifiers.IsMutable)
      throw new Exception($"{memberAccess.Field} is a constant");

    //TODO Think about whether to give pointer types ability to be const or mutable (C const madness) or make a separate keyword for pointers with constant target

    return Right.GetReturnType();
  }
  
  public enum BinaryOp
  {
    Add, Sub, Mul, Div, Mod,
    BitAnd, BitOr, BitXor, Shl, Shr,
    Equals, NotEquals, Greater, Less, GreaterEqual, LessEqual, And, Or, 
    Assign
  }

  public DataType GetReturnType() => Operator switch
  {
    BinaryOp.Add or BinaryOp.Sub or BinaryOp.Mul or BinaryOp.Div or BinaryOp.BitAnd or BinaryOp.BitOr or BinaryOp.BitXor or BinaryOp.Shl or
      BinaryOp.Shr => Arith(),

    BinaryOp.Equals or BinaryOp.NotEquals or BinaryOp.Greater or BinaryOp.Less or BinaryOp.GreaterEqual or BinaryOp.LessEqual or BinaryOp.And or
      BinaryOp.Or => BooleanType.INSTANCE,

    BinaryOp.Assign => Assign(),
    BinaryOp.Mod => Modulus(),

    _ => throw new ArgumentOutOfRangeException(nameof(Operator)),
  };
}
