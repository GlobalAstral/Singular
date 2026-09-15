using System.Diagnostics;
using System.Text;
using Lexer;

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

  public DataType GetReturnType() => new ArrayType(type, new LiteralExpr(new ULongLiteral((ulong)Expressions.Length)));
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
  public DataType GetReturnType() => References.GetFunctionType(Function.ReturnType, [.. Function.Arguments.Select(a => a.Type)], Function.Variadic);
  public override string ToString() => $"{Function}";
}

public class Lambda(Variable[] arguments, DataType? retType, Statement body, bool variadic) : Expression
{
  public Variable[] Arguments {get;} = arguments;
  public DataType? ReturnType {get;} = retType;
  public Statement Body {get;} = body;
  public bool Variadic {get;} = variadic;
  public DataType GetReturnType() => References.GetFunctionType(ReturnType, [ .. Arguments.Select(a => a.Type) ], Variadic);
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
    Minus, Not, BitNot, PreInc, PreDec, Deref, Ref, MutRef, Sizeof, IsNull,
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

  private DataType MutRef()
  {
    if (BinaryExpr.IsNotLValue(Base))
      throw new Exception($"{Base} is not a modifiable lvalue");
    if (Base is IdentifierExpression expr && !expr.Variable.Modifiers.IsMutable)
      throw new Exception($"Variable {expr.Variable} is not mutable and cannot be a mutable pointee");
    return References.GetPointerType(Base.GetReturnType(), true);
  }

  public DataType GetReturnType() => Operator switch
  {
    UnaryOperator.Minus => Signed(),
    UnaryOperator.Not => BooleanType.INSTANCE,
    UnaryOperator.BitNot => Numeric(),
    UnaryOperator.PreInc => Numeric(),
    UnaryOperator.PreDec => Numeric(),
    UnaryOperator.Deref => Deref(),
    UnaryOperator.Ref => References.GetPointerType(Base.GetReturnType(), false),
    UnaryOperator.MutRef => MutRef(),
    UnaryOperator.Sizeof => ULongType.INSTANCE,
    UnaryOperator.IsNull => BooleanType.INSTANCE,

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

  public static bool IsBinaryOpAssignable(BinaryOp op) => op switch
  {
    BinaryOp.Mul
    or BinaryOp.Div
    or BinaryOp.Mod
    or BinaryOp.Add
    or BinaryOp.Sub
    or BinaryOp.Shl
    or BinaryOp.Shr
    or BinaryOp.BitAnd
    or BinaryOp.BitXor
    or BinaryOp.BitOr
    or BinaryOp.And
    or BinaryOp.Or
      => true,
    _ => false
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
    if ((!DataType.IsNumeric(LeftType) && !LeftType.Matches<PointerType>() && !LeftType.Matches<DynamicType>()) || (!DataType.IsNumeric(RightType) && !RightType.Matches<PointerType>() && !RightType.Matches<DynamicType>()))
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
  public static bool IsNotLValue(Expression expr) => expr is not IdentifierExpression && expr is not IndexExpr && expr is not MemberAccess && !(expr is UnaryExpression u && u.Operator == UnaryExpression.UnaryOperator.Deref);
  private DataType Assign()
  {
    if (IsNotLValue(Left))
      throw new Exception($"{Left} is not a modifiable lvalue");

    if (Left is IdentifierExpression ident && !ident.Variable.Modifiers.IsMutable)
      throw new Exception($"{ident.Variable} is a constant");
    
    if (Left is MemberAccess memberAccess && !memberAccess.Field.Modifiers.IsMutable)
      throw new Exception($"{memberAccess.Field} is a constant");

    if (Left is UnaryExpression unary)
    {
      PointerType pointer = (unary.Base.GetReturnType() as PointerType)!;
      if (!pointer.Mutable)
        throw new Exception($"{unary.Base} returns a pointer to a constant pointee");
    }

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

public class ErrorExpr(string Err) : Expression
{
  public string Err {get;} = Err;
  public DataType GetReturnType() => ErrorType.INSTANCE;
}

public class ErrorUnionSuccessExpr(ErrorUnion ErrorUnion, Expression Success) : Expression
{
  public ErrorUnion ErrorUnion {get;} = ErrorUnion;
  public Expression Success {get;} = Success;
  public DataType GetReturnType() => ErrorUnion;
}

public class ErrorUnionFailExpr(ErrorUnion ErrorUnion, Expression Fail) : Expression
{
  public ErrorUnion ErrorUnion {get;} = ErrorUnion;
  public Expression Fail {get;} = Fail;
  public DataType GetReturnType() => ErrorUnion;
}

public class TryDefaultExpression(DataType returnType, Expression expression, Expression def) : Expression
{
  public DataType ReturnType {get;} = returnType;
  public Expression Expression {get;} = expression;
  public Expression Default {get;} = def;
  public DataType GetReturnType() => ReturnType;
}

public class TryCatchExpression(DataType returnType, Expression expression, Variable? err, Statement cat) : Expression
{
  public DataType ReturnType {get;} = returnType;
  public Expression Expression {get;} = expression;
  public Variable? Error {get;} = err;
  public Statement Catch {get;} = cat;
  public DataType GetReturnType() => ReturnType;
}

public partial class Parser
{
  private enum ExpressionKind
  {
    FULL,
    BASE,
  }
  private bool PeekUnary() => (Peek(Token.Get(Token.Type.PLUS)) && Peek(Token.Get(Token.Type.PLUS), 1)) || Peek(Token.Get(Token.Type.MINUS)) ||
    Peek(Token.Get(Token.Type.EXCLAMATION)) || Peek(Token.Get(Token.Type.TILDE)) || Peek(Token.Get(Token.Type.STAR)) || Peek(Token.Get(Token.Type.AMPER)) ||
    Peek(Token.Get(Token.Type.SIZEOF)) || Peek(Token.Get(Token.Type.ISNULL));

  private UnaryExpression ParseUnary()
  {
    UnaryExpression.UnaryOperator? op = null;
    if (Peek(Token.Get(Token.Type.PLUS)) && Peek(Token.Get(Token.Type.PLUS), 1))
    {
      Consume(2);
      op = UnaryExpression.UnaryOperator.PreInc;
    }
    else if (TryConsume(Token.Get(Token.Type.MINUS))) {
      if (TryConsume(Token.Get(Token.Type.MINUS)))
        op = UnaryExpression.UnaryOperator.PreDec;
      else
        op = UnaryExpression.UnaryOperator.Minus;
    }
    else if (TryConsume(Token.Get(Token.Type.EXCLAMATION)))
      op = UnaryExpression.UnaryOperator.Not;
    else if (TryConsume(Token.Get(Token.Type.TILDE)))
      op = UnaryExpression.UnaryOperator.BitNot;
    else if (TryConsume(Token.Get(Token.Type.STAR)))
      op = UnaryExpression.UnaryOperator.Deref;
    else if (TryConsume(Token.Get(Token.Type.AMPER)))
      op = TryConsume(Token.Get(Token.Type.MUT)) ? UnaryExpression.UnaryOperator.MutRef : UnaryExpression.UnaryOperator.Ref;
    else if (TryConsume(Token.Get(Token.Type.SIZEOF)))
      op = UnaryExpression.UnaryOperator.Sizeof;
    else if (TryConsume(Token.Get(Token.Type.ISNULL)))
      op = UnaryExpression.UnaryOperator.IsNull;

    if (op == null)
      throw new Exception("Expected Unary Operator");
    
    extendedExpr = false;
    Expression e = ParseExpression(null, ExpressionKind.BASE);
    UnaryExpression r = new(e, (UnaryExpression.UnaryOperator)op);
    return r;
  }

  private Expression? ParsePostExpression(Expression @base)
  {
    if (!extendedExpr)
    {
      extendedExpr = true;
      return null;
    }

    DataType baseType = @base.GetReturnType();

    if (TryConsume(Token.Get(Token.Type.DOT)))
    {
      if (!baseType.Matches<CompositeType>() && !(baseType.Matches<PointerType>(out var ptr) && ptr!.Target.Matches<CompositeType>())) Error("Cannot access member of non composite or composite pointer type");
      CompositeType type = baseType.Matches<PointerType>(out var p) ? (CompositeType) p!.Target : (CompositeType) baseType;
      string name = NoMangle();
      Variable? field = type.Comp.Fields.Find(f => f.Name == name);
      if (field == null) Error($"{type.Comp.Name} does not have a member named {name}");
      return new MemberAccess(@base, field);
    }

    if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
    {
      DataType target;
      if (baseType.Matches<ArrayType>(out var arr))
        target = arr!.Elements;
      else if (baseType.Matches<PointerType>(out var ptr))
        target = ptr!.Target;
      else if (baseType.Matches<StringType>())
        target = CharType.INSTANCE;
      else
      {
        Error("Cannot index non-array type or non-pointer type or non-string type");
        throw new UnreachableException();
      }
      Token[] body = (Token[]) Consume().value!;
      Expression index = Switch(body, () => ParseExpression(ULongType.INSTANCE));
      return new IndexExpr(@base, index, target);
    }

    if (Peek(Token.Get(Token.Type.PAREN_BLOCK)))
    {
      if (!baseType.Matches<FunctionType>())
        Error("Cannot call a non-function type");
      FunctionType functionType = (FunctionType) baseType;
      Token[] body = (Token[]) Consume().value!;

      List<Expression> values = [];

      int argIndex = 0;
      Switch(body, () =>
      {
        if (!functionType.Variadic && argIndex >= functionType.Arguments.Length)
          Error($"Invalid function arguments. Provided {argIndex+1} Expected {functionType.Arguments.Length}");
        DataType? type = argIndex < functionType.Arguments.Length ? functionType.Arguments[argIndex] : null;
        values.Add(ParseExpression(type));
        argIndex++;
      }, Token.Get(Token.Type.COMMA));
      
      if (values.Count < functionType.Arguments.Length)
        Error($"Invalid function arguments. Provided {values.Count} Expected {functionType.Arguments.Length}");
      
      if (functionType.Return == null && IgnoringExpression == 0)
        Error("Not returned value not ignored as it ought to be");
      
      return new FunctionCall(@base, [.. values], functionType.Return!);
    }

    if (TryConsume(Token.Get(Token.Type.AS)))
    {
      DataType type = ParseType();
      return new Cast(@base, type);
    }

    if (TryConsume(Token.Get(Token.Type.BITCAST)))
    {
      DataType type = ParseType();
      return new BitCast(@base, type);
    }

    if (TryConsume(Token.Get(Token.Type.QUESTION)))
    {
      if (!baseType.Matches<BooleanType>()) Error("Condition cannot be a non-boolean type");
      Expression success = ParseExpression(null);
      TryConsumeError(Token.Get(Token.Type.COLON));
      Expression fail = ParseExpression(success.GetReturnType());
      return new TernaryOperator(@base, success, fail);
    }

    if (Peek(Token.Get(Token.Type.PLUS)) && Peek(Token.Get(Token.Type.PLUS), 1)) {
      Consume(2);
      if (!DataType.IsNumeric(baseType))
        Error("Cannot use a numeric operator on a non-numeric type");
      return new PostIncrement(@base, 1);
    }

    if (Peek(Token.Get(Token.Type.MINUS)) && Peek(Token.Get(Token.Type.MINUS), 1)) {
      Consume(2);
      if (!DataType.IsNumeric(baseType))
        Error("Cannot use a numeric operator on a non-numeric type");
      return new PostIncrement(@base, -1);
    }

    return null;
  }

  private BinaryExpr.BinaryOp? PeekBinary()
  {
    if (TryConsume(Token.Get(Token.Type.PLUS)))
      return BinaryExpr.BinaryOp.Add;
    if (TryConsume(Token.Get(Token.Type.MINUS)))
      return BinaryExpr.BinaryOp.Sub;
    if (TryConsume(Token.Get(Token.Type.STAR)))
      return BinaryExpr.BinaryOp.Mul;
    if (TryConsume(Token.Get(Token.Type.SLASH)))
      return BinaryExpr.BinaryOp.Div;
    if (TryConsume(Token.Get(Token.Type.PERCENT)))
      return BinaryExpr.BinaryOp.Mod;
    if (TryConsume(Token.Get(Token.Type.AMPER)))
    {
      if (TryConsume(Token.Get(Token.Type.AMPER)))
        return BinaryExpr.BinaryOp.And;
      return BinaryExpr.BinaryOp.BitAnd;
    }
    if (TryConsume(Token.Get(Token.Type.PIPE)))
    {
      if (TryConsume(Token.Get(Token.Type.PIPE)))
        return BinaryExpr.BinaryOp.Or;
      return BinaryExpr.BinaryOp.BitOr;
    }
    if (TryConsume(Token.Get(Token.Type.CARET)))
      return BinaryExpr.BinaryOp.BitXor;
    if (TryConsume(Token.Get(Token.Type.LANGLE)))
    {
      if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
        return BinaryExpr.BinaryOp.LessEqual;
      if (TryConsume(Token.Get(Token.Type.LANGLE)))
        return BinaryExpr.BinaryOp.Shl;
      return BinaryExpr.BinaryOp.Less;
    }
    if (TryConsume(Token.Get(Token.Type.RANGLE)))
    {
      if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
        return BinaryExpr.BinaryOp.GreaterEqual;
      if (TryConsume(Token.Get(Token.Type.RANGLE)))
        return BinaryExpr.BinaryOp.Shr;
      return BinaryExpr.BinaryOp.Greater;
    }
    if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
    {
      if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
        return BinaryExpr.BinaryOp.Equals;
      return BinaryExpr.BinaryOp.Assign;
    }
    if (Peek(Token.Get(Token.Type.EXCLAMATION)) && Peek(Token.Get(Token.Type.EQUALS_SYMBOL), 1))
    {
      Consume(2);
      return BinaryExpr.BinaryOp.NotEquals;
    }
    
    return null;
  }

  private Expression ParseBinary(Expression left, BinaryExpr.BinaryOp op)
  {
    bool compound = false;
    if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
    {
      if (!BinaryExpr.IsBinaryOpAssignable(op)) Error($"Cannot compound operator {op} into an assignment");
      compound = true;
    }

    Expression right = ParseExpression(null);
    Expression result = new BinaryExpr(left, right, op);

    if (right is BinaryExpr rbin && BinaryExpr.Precedence(op) > BinaryExpr.Precedence(rbin.Operator))
    {
      Expression l = new BinaryExpr(left, rbin.Left, op);
      result = new BinaryExpr(l, rbin.Right, rbin.Operator);
    }

    if (compound && BinaryExpr.IsNotLValue(left))
      Error($"{left} is not a modifiable lvalue");

    if (compound)
      result = new BinaryExpr(left, result, BinaryExpr.BinaryOp.Assign);

    return result;
  }

  private Expression ParseBaseExpr()
  {
    if (Peek(Token.Get(Token.Type.PAREN_BLOCK)))
      return Switch((Token[])Consume().value!, () => ParseExpression(null));
    if (PeekUnary())
      return ParseUnary();
    if (Peek(Token.Get(Token.Type.LITERAL)))
      return new LiteralExpr(Literal.ParseLiteral((string)Consume().value!));
    if (TryConsume(Token.Get(Token.Type.NULL)))
    {
      if (typeCheckerContext.Count == 0) Error("Cannot infer type of null value");
      return typeCheckerContext.Peek()!.GetNull();
    }
    if (PeekIdentifier())
    {
      string name = Mangle(SymbolType.Variable);
      Function? fn = functions.Find(f => f.Name == name);

      if (declared_errors.Contains(name))
        return new ErrorExpr(name);
      if (fn != null)
        return new FunctionPointer(fn);
      Variable? variable = SearchVariable(name);
      if (variable == null)
        Error($"Variable {name} does not exist");
      return new IdentifierExpression(variable);
    }
    if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
    {
      Token[] body = (Token[]) Consume().value!;
      List<Expression> expressions = [];
      DataType? locked_type = typeCheckerContext.Peek();
      if (locked_type == null)  
        Error("Cannot infer type from Array Literal");
      if (locked_type is not ArrayType)
        Error("Cannot initialize non-array type with ArrayLiteral");
      ArrayType arr = (locked_type as ArrayType)!;
      locked_type = arr.Elements;
      Switch(body, () =>
      {
        Expression e = ParseExpression(locked_type);
        expressions.Add(e);
      }, Token.Get(Token.Type.COMMA));
      if (arr.Size != null)
        Error("Cannot specify array size when initializing it with an ArrayLiteral");
      arr.Size = new LiteralExpr(new ULongLiteral((ulong) expressions.Count));
      return new ArrayLiteral(locked_type!, [.. expressions]);
    }
    if (Peek(Token.Get(Token.Type.CURLY_BLOCK)))
    {
      Token[] body = (Token[]) Consume().value!;
      DataType? required = typeCheckerContext.Peek();
      if (required == null || !required.Matches<CompositeType>()) Error($"Cannot initialize a non-composite type to a composite literal value");

      CompositeType composite = (CompositeType) required;
      
      bool named = false;
      int field_index = 0;
      Dictionary<string, Expression> keyValues = [];
      
      Switch(body, () =>
      {
        if (TryConsume(Token.Get(Token.Type.DOT)))
        {
          named = true;
          string ident = NoMangle();
          TryConsumeError(Token.Get(Token.Type.EQUALS_SYMBOL));
          Variable? found = composite.Comp.Fields.Find(v => v.Name == ident);
          if (found == null) Error($"Type {composite} has no field named {ident}");
          Expression e = ParseExpression(found.Type);
          keyValues[ident] = e;
        }
        else
        {
          if (named) Error("Cannot mix named and unnamed initialization");
          if (field_index >= composite.Comp.Fields.Count) Error("Too many values for initialization");
          Variable variable = composite.Comp.Fields[field_index++];
          Expression e = ParseExpression(variable.Type);
          keyValues[variable.Name] = e;
        }
      }, Token.Get(Token.Type.COMMA));

      return new CompositeLiteral(composite, keyValues);
    }
    if (TryConsume(Token.Get(Token.Type.FUN)))
    {
      (Variable[] arguments, bool variadic) = ParseArgs();
      DataType? retType = null;
      if (TryConsume(Token.Get(Token.Type.COLON)))
        retType = ParseType();
      Statement body = ProcessOne();
      return new Lambda(arguments, retType, body, variadic);
    }
    if (Peek(Token.Get(Token.Type.RAWC)))
    {
      string code = (string) Consume().value!;
      DataType? retType = typeCheckerContext.Peek();
      if ((typeCheckerContext.Count == 0 || retType == null) && IgnoringExpression == 0)
        Error("Expression is not ignored as it ought to be");
      return new RawExpr(retType!, code);
    }
    if (TryConsume(Token.Get(Token.Type.TRY)))
    {
      TryingExpression++;
      Expression expr = ParseExpression(null, ExpressionKind.BASE);
      TryingExpression--;
      DataType res = expr.GetReturnType();
      if (!res.Matches(out ErrorUnion? union))
        Error("try expression cannot be applied to a type that is not an error union");
      if (TryConsume(Token.Get(Token.Type.DEFAULT)))
        return new TryDefaultExpression(union!.Success, expr, ParseExpression(union.Success));

      TryConsumeError(Token.Get(Token.Type.CATCH));
      Variable? get_err()
      {
        if (PeekIdentifier())
          return new Variable(new ModifierHandler(), ErrorType.INSTANCE, Mangle(SymbolType.LocalVariableDecl));
        return null;
      }
      Variable? variable = get_err();

      if (variable != null)
      {
        PushSnapshot();
        AddVariable(variable);
      }

      Statement body = ProcessOne();

      if (variable != null)
        PopSnapshot();

      return new TryCatchExpression(union!.Success, expr, variable, body);
    }
    Error("Expected Expression");
    throw new UnreachableException();
  }

  private Expression ParseExpression(ExpressionKind kind = ExpressionKind.FULL)
  {
    Expression expression = ParseBaseExpr();

    Expression? result = ParsePostExpression(expression);
    while (result != null)
    {
      expression = result;
      result = ParsePostExpression(expression);
    }

    BinaryExpr.BinaryOp? op = PeekBinary();
    if (kind == ExpressionKind.FULL && op != null)
      expression = ParseBinary(expression, (BinaryExpr.BinaryOp) op);
    
    DataType expr_type = expression!.GetReturnType();
    DataType? check_type = typeCheckerContext.Peek();

    if (expr_type.Matches<ErrorUnion>() && TryingExpression == 0)
      Warn("Returned error union not handled as it should be");
    
    if (check_type != null && !check_type.CanAccept(expr_type))
      Error($"Expected {check_type} got {expr_type} instead");

    if (check_type != null && check_type.Matches<ErrorUnion>(out var errorUnion))
    {
      if (errorUnion!.Success.CanAccept(expr_type))
        return new ErrorUnionSuccessExpr(errorUnion, expression);
      if (expr_type.Matches<ErrorType>())
        return new ErrorUnionFailExpr(errorUnion, expression);
    }

    return expression;
  }
}
