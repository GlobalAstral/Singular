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
      dataType = References.GetPointerType(ParseType());
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
      if (composites.TryGetValue(ident, out var value))
        dataType = References.GetCompositeType(ident, value);
    }
    
    if (dataType == null)
      Error("Expected Type");
    
    if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
    {
      Token[] temp = (Token[])Consume().value!;
      if (temp.Length != 0)
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

  protected Variable? SearchVariable(string name)
  {
    Variable? found = locals.Find(v => v.Name == name);
    if (found != null)
      return found;
    if (currentContext.Count > 0 && currentContext.Peek() is FunctionContext context)
    {
      found = context.Arguments.ToList().Find(v => v.Name == name);
      if (found != null)
        return found;
    }
    return null;
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
      op = UnaryExpression.UnaryOperator.Ref;
    else if (TryConsume(Token.Get(Token.Type.SIZEOF)))
      op = UnaryExpression.UnaryOperator.Sizeof;
    if (op == null)
      throw new Exception("Expected Unary Operator");
    
    Expression e = ParseExpression(null);
    UnaryExpression r = new(e, (UnaryExpression.UnaryOperator)op);
    r.GetReturnType();
    return r;
  }

  private Expression ParseExpression(DataType? required)
  {
    typeCheckerContext.Push(required);
    Expression ret = ParseExpression();
    typeCheckerContext.Pop();
    return ret;
  }

  private Expression ParseExpression()
  {
    Expression? expression = null;
    if (PeekUnary())
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
      if (locked_type is not ArrayType) Error("Cannot initialize a non-array type to an array literal value");
      locked_type = ((ArrayType) locked_type).Elements;
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
      if (required is not CompositeType) Error($"Cannot initialize a non-composite type to a composite literal value");

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
    
    DataType expr_type = expression!.GetReturnType();
    DataType? check_type = typeCheckerContext.Peek();
    
    if (check_type != null && expr_type != check_type)
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
