using System.Diagnostics;
using System.Text;
using Lexer;

namespace Parser;

public class ModifierHandler
{
  public bool IsStatic = false;
  public bool IsMutable = false;
  public ModifierHandler Static()
  {
    IsStatic = true;
    return this;
  }

  public ModifierHandler Mutable()
  {
    IsMutable = true;
    return this;
  }

  public override bool Equals(object? obj)
  {
    if (obj is not ModifierHandler other)
      return false;
    
    if (IsStatic != other.IsStatic)
      return false;
    
    if (IsMutable != other.IsMutable)
      return false;
    
    return true;
  }

  public override int GetHashCode() => HashCode.Combine(IsStatic, IsMutable);

  public static bool operator ==(ModifierHandler a, ModifierHandler b) => a.Equals(b);
  public static bool operator !=(ModifierHandler a, ModifierHandler b) => !a.Equals(b);

  public override string ToString()
  {
    string s = IsStatic ? "static" : ""; 
    string m = IsMutable ? "mutable" : ""; 
    string temp = $"{s} {m}".Trim();
    return $"[{temp}]";
  }
}

public partial class Parser
{
  protected T WithModifiers<T>(Func<ModifierHandler, T> action)
  {
    ModifierHandler handler = new();
    
    if (TryConsume(Token.Get(Token.Type.STATIC)))
      handler.Static();
    
    if (TryConsume(Token.Get(Token.Type.MUTABLE)))
      handler.Mutable();
    
    return action(handler);
  }

  protected void WithModifiers(Action<ModifierHandler> action) => WithModifiers(handler =>
  {
    action(handler);
    return 0;
  });

  protected ModifierHandler GetModifiers(Action<ModifierHandler> sanitize)
  {
    return WithModifiers(handler =>
    {
      sanitize(handler);
      return handler;
    });
  }
  
  protected DataType ParseType()
  {
    DataType? dataType = null;
    if (TryConsume(Token.Get(Token.Type.STAR)))
    {
      bool mutable = TryConsume(Token.Get(Token.Type.MUTABLE));
      dataType = References.GetPointerType(ParseType(), mutable);
    }
    else if (TryConsume(Token.Get(Token.Type.BYTE)))
      dataType = ByteType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.CHAR)))
      dataType = CharType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.USHORT)))
      dataType = UShortType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.SHORT)))
      dataType = ShortType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.UINT)))
      dataType = UIntType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.INT)))
      dataType = IntType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.ULONG)))
      dataType = ULongType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.LONG)))
      dataType = LongType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.BOOLEAN)))
      dataType = BooleanType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.FLOAT)))
      dataType = FloatType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.DOUBLE)))
      dataType = DoubleType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.DYNAMIC)))
      dataType = DynamicType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.FUN)))
    {
      DataType[] args = [.. ParseArgs().Select(v => v.Type)];
      DataType? result = null;
      if (TryConsume(Token.Get(Token.Type.COLON)))
        result = ParseType();
      return References.GetFunctionType(result, args);
    }
    else if (Peek(Token.Get(Token.Type.IDENTIFIER)))
    {
      string ident = ParseIdentifier();
      if (aliases.TryGetValue(ident, out var value))
        dataType = References.GetAliasType(ident, value);
      else if (composites.TryGetValue(ident, out var val))
        dataType = References.GetCompositeType(ident, val);
    }
    
    if (dataType == null)
      Error("Expected Type");
    
    if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
    {
      Token[] temp = (Token[])Consume().value!;
      if (temp.Length == 0)
        Error("Invalid Array Type");
      Expression size = Switch(temp, () => ParseExpression(ULongType.INSTANCE));
      dataType = References.GetArrayType(dataType, size);
    }
    return dataType;
  }

  protected Variable[] ParseArgs()
  {
    Token[] args = (Token[])TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
    List<Variable> arguments = [];
    Switch(args, () => WithModifiers(handler =>
    {
      if (handler.IsStatic)
        Error("Argument cannot be static");

      DataType t = ParseType();
      string ident = ParseIdentifier();
      if (arguments.Any(v => v.Name == ident))
        Error($"Function type cannot have duplicate arguments");
      arguments.Add(new Variable(handler, t, ident));
    }), Token.Get(Token.Type.COMMA));
    return [.. arguments];
  }

  private string[] ParseGenerics()
  {
    if (Peek(Token.Get(Token.Type.ANGLE_BLOCK)))
    {
      Token[] generics_body = (Token[])Consume().value!;
      List<string> generics = [];
      Switch(generics_body, () => generics.Add((string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!), Token.Get(Token.Type.COMMA));
      return [.. generics];
    }
    return [];
  }

  protected string MangleIdentifier()
  {
    string ident = (string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;

    StringBuilder builder = new();

    if (Peek(Token.Get(Token.Type.COLON)) && Peek(Token.Get(Token.Type.COLON)))
    {
      builder.Append(ident);
      while (Peek(Token.Get(Token.Type.COLON)) && Peek(Token.Get(Token.Type.COLON)))
      {
        Consume(2);
        builder.Append($"_{(string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!}");
      }
      return builder.ToString();
    }

    if (currentContext.Count != 0 && currentContext.Peek() is CompositeContext context)
      return $"{context.Comp.Name}_{ident}";


    foreach (string namesp in namespaces.Reverse())
      builder.Append($"{namesp}_");

    builder.Append(ident);

    return builder.ToString();
  }

  protected string ParseIdentifier()
  {
    StringBuilder builder = new();
    builder.Append((string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!);

    while (Peek(Token.Get(Token.Type.COLON)) && Peek(Token.Get(Token.Type.COLON), 1))
    {
      Consume(2);
      builder.Append($"_{(string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!}");
    }

    return builder.ToString();
  }

  protected bool InFunction(out FunctionContext? context)
  {
    Context current = currentContext.Peek();
    if (currentContext.Count > 0 && current is ScopeContext ctx && ctx.FunctionContext != null)
    {
      context = current as FunctionContext;
      return true;
    }
    context = null;
    return false;
  }

  protected bool InFunction() => InFunction(out var _);

  protected bool InScope(out ScopeContext? context)
  {
    Context current = currentContext.Peek();
    if (currentContext.Count > 0 && current is ScopeContext ctx)
    {
      context = ctx;
      return true;
    }
    context = null;
    return false;
  }

  protected bool InScope() => InScope(out var _);

  protected Variable? SearchVariable(string name)
  {
    Variable? found;
    foreach (ScopeContext scope in activeScopes)
    {
      found = scope!.Locals.Find(v => v.Name == name);
      if (found != null)
        return found;

      if (scope.FunctionContext != null)
      {
        found = scope.FunctionContext.Arguments.ToList().Find(v => v.Name == name);
        if (found != null)
          return found;
      }
    }

    found = globals.Find(v => v.Name == name);
    if (found != null)
      return found;

    return null;
  }

  protected void AddVariable(Variable variable)
  {
    if (SearchVariable(variable.Name) != null)
      Error($"Variable {variable.Name} already exists");
    
    if (InScope(out var scope))
    {
      scope!.Locals.Add(variable);
      return;
    }

    globals.Add(variable);
  }

  private Statement ParseFunction() {
    ModifierHandler modifiers = GetModifiers(handler => { if (handler.IsMutable) Error("Function cannot be mutable"); });

    string name = MangleIdentifier();
    Variable[] args = ParseArgs();
    DataType? retType = TryConsume(Token.Get(Token.Type.COLON)) ? ParseType() : null;
    
    currentContext.Push(new FunctionContext(retType, args));
    
    Statement? body = TryConsume(Token.Get(Token.Type.SEMI)) ? null : ProcessOne();
    
    currentContext.Pop();
    
    Function f = new(modifiers, name, args, retType, body);
    Function? found = functions.Find(ele => ele.Equals(f));
    
    if (found == null)
    {
      functions.Add(f);
      return new FunctionDecl(f);
    }
    if (found.Body == null)
    {
      found.Body = f.Body;
      return new FunctionDecl(found);
    }
    Error($"Function {name} already exists");
    return null;
  }

  private Statement ParseComposite(Composite.Type kind, Func<Composite, Statement> factory) {
    string ident = MangleIdentifier();
    Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
    List<Statement> group = [];
    Composite s = Switch(body, () =>
    {
      Composite s = new(ident, [], [], kind);
      Context ctx = new CompositeContext(s);
      composites[ident] = s;
      while (HasPeek())
      {
        if (TryConsume(Token.Get(Token.Type.FUN)))
        {
          currentContext.Push(ctx);
          Statement func = ParseFunction();
          currentContext.Pop();
          group.Add(func);
        }
        else
        {
          ModifierHandler modifiers = GetModifiers(handler => { if (!handler.IsStatic) handler.Mutable(); });

          DataType type = ParseType();
          string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
          
          bool isStatic = modifiers.IsStatic;

          Variable variable = new(modifiers, type, name);
          if (isStatic)
          {
            if (s.Statics.Keys.Any(v => v.Name == variable.Name))
              Error($"{kind} static field {variable.Name} already exists");

            Expression? val = null;
            if (TryConsume(Token.Get(Token.Type.EQUALS)))
              val = ParseExpression(variable.Type);
            s.Statics[variable] = val;
          }
          else
          {
            if (s.Fields.Any(v => v.Name == variable.Name))
              Error($"{kind} non-static field {variable.Name} already exists");
            s.Fields.Add(variable);
          }
          TryConsumeError(Token.Get(Token.Type.SEMI));
        }
      }
      return s; 
    });
    group.Insert(0, factory(s));
    return new Group([.. group]);
  }

  private bool PeekUnary() => (Peek(Token.Get(Token.Type.PLUS)) && Peek(Token.Get(Token.Type.PLUS), 1)) || Peek(Token.Get(Token.Type.MINUS)) ||
    Peek(Token.Get(Token.Type.EXCLAMATION)) || Peek(Token.Get(Token.Type.TILDE)) || Peek(Token.Get(Token.Type.STAR)) || Peek(Token.Get(Token.Type.AMPER)) ||
    Peek(Token.Get(Token.Type.SIZEOF));

  private Expression ParseUnary()
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
      op = TryConsume(Token.Get(Token.Type.MUTABLE)) ? UnaryExpression.UnaryOperator.MutRef : UnaryExpression.UnaryOperator.Ref;
    else if (TryConsume(Token.Get(Token.Type.SIZEOF)))
      op = UnaryExpression.UnaryOperator.Sizeof;
    if (op == null)
      throw new Exception("Expected Unary Operator");
    
    extendedExpr = false;
    Expression e = ParseExpression(null);
    UnaryExpression r = new(e, (UnaryExpression.UnaryOperator)op);
    return r;
  }

  private Expression ParseExpression(DataType? required)
  {
    typeCheckerContext.Push(required);
    Expression ret = ParseExpression();
    typeCheckerContext.Pop();
    return ret;
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
      if (!baseType.Matches<CompositeType>()) Error("Cannot access member of non-composite type");
      CompositeType type = (CompositeType) baseType;
      string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
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
      else
      {
        Error("Cannot index non-array type or non-pointer type");
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

      Switch(body, () => values.Add(ParseExpression()), Token.Get(Token.Type.COMMA));

      if (values.Count != functionType.Arguments.Length)
        Error($"Invalid function arguments. Provided {values.Count} Expected {functionType.Arguments.Length}");

      if (!values.Zip(functionType.Arguments).All(pair => pair.First.GetReturnType() == pair.Second))
        Error("Invalid types for function arguments");
      
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

    BinaryExpr.BinaryOp? op = PeekBinary();
    if (op != null)
      return ParseBinary(@base, (BinaryExpr.BinaryOp) op);

    return null;
  }

  private static bool IsRightAssociative(BinaryExpr.BinaryOp op) => op == BinaryExpr.BinaryOp.Assign;
  private static bool ShouldRotate(BinaryExpr.BinaryOp current, BinaryExpr.BinaryOp right)
  {
      uint cp = BinaryExpr.Precedence(current);
      uint rp = BinaryExpr.Precedence(right);

      if (cp > rp)
        return true;

      if (cp < rp)
        return false;

      return !IsRightAssociative(current);
  }

  private Expression ParseBinary(Expression left, BinaryExpr.BinaryOp op)
  {
    bool compound = false;
    if (TryConsume(Token.Get(Token.Type.EQUALS)))
    {
      if (!BinaryExpr.IsBinaryOpAssignable(op)) Error($"Cannot compound operator {op} into an assignment");
      compound = true;
    }

    Expression right = ParseExpression(null);
    Expression result = new BinaryExpr(left, right, op);

    if (right is BinaryExpr rbin && ShouldRotate(op, rbin.Operator))
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
      if (TryConsume(Token.Get(Token.Type.EQUALS)))
        return BinaryExpr.BinaryOp.LessEqual;
      if (TryConsume(Token.Get(Token.Type.LANGLE)))
        return BinaryExpr.BinaryOp.Shl;
      return BinaryExpr.BinaryOp.Less;
    }
    if (TryConsume(Token.Get(Token.Type.RANGLE)))
    {
      if (TryConsume(Token.Get(Token.Type.EQUALS)))
        return BinaryExpr.BinaryOp.GreaterEqual;
      if (TryConsume(Token.Get(Token.Type.RANGLE)))
        return BinaryExpr.BinaryOp.Shr;
      return BinaryExpr.BinaryOp.Greater;
    }
    if (TryConsume(Token.Get(Token.Type.EQUALS)))
    {
      if (TryConsume(Token.Get(Token.Type.EQUALS)))
        return BinaryExpr.BinaryOp.Equals;
      return BinaryExpr.BinaryOp.Assign;
    }
    if (Peek(Token.Get(Token.Type.EXCLAMATION)) && Peek(Token.Get(Token.Type.EQUALS), 1))
    {
      Consume(2);
      return BinaryExpr.BinaryOp.NotEquals;
    }
    
    return null;
  }

  private Expression ParseExpression()
  {
    Expression? expression = null;
    if (Peek(Token.Get(Token.Type.PAREN_BLOCK)))
      expression = Switch((Token[])Consume().value!, ParseExpression);
    else if (PeekUnary())
      expression = ParseUnary();
    else if (Peek(Token.Get(Token.Type.LITERAL)))
    {
      string lit = (string)Consume().value!;
      expression = new LiteralExpr(Literal.ParseLiteral(lit));
    }
    else if (TryConsume(Token.Get(Token.Type.NULL)))
    {
      if (typeCheckerContext.Count == 0) Error("Cannot infer type of null value");
      expression = typeCheckerContext.Peek()!.GetNull();
    }
    else if (Peek(Token.Get(Token.Type.IDENTIFIER)))
    {
      string name = ParseIdentifier();
      Function? fn = functions.Find(f => f.Name == name);

      if (fn != null)
        expression = new FunctionPointer(fn);
      else
      {
        Variable? variable = SearchVariable(name);
        if (variable == null)
          Error($"Variable {name} does not exist");
        expression = new IdentifierExpression(variable);
      }
    }
    else if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
    {
      Token[] body = (Token[]) Consume().value!;
      List<Expression> expressions = [];
      DataType? locked_type = typeCheckerContext.Peek();
      Switch(body, () =>
      {
        Expression e = ParseExpression(locked_type);
        locked_type ??= e.GetReturnType();
        expressions.Add(e);
      }, Token.Get(Token.Type.COMMA));
      if (locked_type == null) Error("Cannot infer type from Array Literal");
      expression = new ArrayLiteral(locked_type!, [.. expressions]);
    }
    else if (Peek(Token.Get(Token.Type.CURLY_BLOCK)))
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
          string ident = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
          TryConsumeError(Token.Get(Token.Type.EQUALS));
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

      expression = new CompositeLiteral(composite, keyValues);
    }
    else if (TryConsume(Token.Get(Token.Type.FUN)))
    {
      Variable[] arguments = ParseArgs();
      DataType? retType = null;
      if (TryConsume(Token.Get(Token.Type.COLON)))
        retType = ParseType();
      Statement body = ProcessOne();
      expression = new Lambda(arguments, retType, body);
    }
    else Error("Expected Expression");

    Expression? result = ParsePostExpression(expression);
    while (result != null)
    {
      expression = result;
      result = ParsePostExpression(expression);
    }
    
    DataType expr_type = expression!.GetReturnType();
    DataType? check_type = typeCheckerContext.Peek();
    
    if (check_type != null && !check_type.CanAccept(expr_type))
      Error($"Expected {check_type} got {expr_type} instead");

    return expression;
  }

  public void SavePeek() => saved_peek.Push(peek);
  public void RestorePeek() => peek = saved_peek.Pop();
  private readonly Stack<int> saved_peek = [];
  
  protected Statement? Wakeup(Token.Type token, bool consume, Func<Statement?> action, Func<Statement?> else_action)
  {
    Token tok = Token.Get(token);
    if ((consume && TryConsume(tok)) || (!consume && Peek(tok)))
      return action();
    else
      return else_action();
  }
  protected void Semi() => TryConsumeError(Token.Get(Token.Type.SEMI));
}
