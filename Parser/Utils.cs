using System.Text;
using Lexer;

namespace Parser;

public class ModifierHandler
{
  public bool IsStatic = false;
  public bool IsMutable = false;
  public bool IsReadonly = false;

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

  public ModifierHandler Readonly()
  {
    IsReadonly = true;
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
    
    if (IsReadonly != other.IsReadonly)
      return false;
    
    return true;
  }

  public override int GetHashCode() => HashCode.Combine(IsStatic, IsMutable, IsReadonly);

  public static bool operator ==(ModifierHandler a, ModifierHandler b) => a.Equals(b);
  public static bool operator !=(ModifierHandler a, ModifierHandler b) => !a.Equals(b);

  public override string ToString()
  {
    string s = IsStatic ? "static" : ""; 
    string m = IsMutable ? "mutable" : ""; 
    string r = IsReadonly ? "readonly" : "";
    string temp = $"{s} {m} {r}".Trim();
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
    if (TryConsume(Token.Get(Token.Type.BYTE)))
      dataType = ByteType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.CHAR)))
      dataType = CharType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.USHORT)))
      dataType = UShortType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.SHORT)))
      dataType = ShortType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.UINT)))
      dataType = UIntType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.INT)))
      dataType = IntType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.ULONG)))
      dataType = ULongType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.LONG)))
      dataType = LongType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.BOOLEAN)))
      dataType = BooleanType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.FLOAT)))
      dataType = FloatType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.DOUBLE)))
      dataType = DoubleType.INSTANCE;
    if (TryConsume(Token.Get(Token.Type.FUN)))
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
      if (structs.TryGetValue(ident, out var value))
        dataType = References.GetStructType(ident, value);
    }
    
    if (dataType == null)
      Error("Expected Type");
    
    if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
    {
      List<Token> temp = (List<Token>)Consume().value!;
      if (temp.Count != 0)
        Error("Invalid Array Type");
      dataType = References.GetArrayType(dataType);
    }
    return dataType;
  }

  protected Variable[] ParseArgs()
  {
    Token[] args = (Token[])TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
    List<Variable> arguments = [];
    Switch(args, () => WithModifiers(handler =>
    {
      if (handler.IsReadonly)
        Error("Argument cannot be readonly");
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
    StringBuilder builder = new();
    builder.Append((string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!);

    foreach (string namesp in namespaces.Reverse())
      builder.Insert(0, $"{namesp}_");

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
    if (Peek(Token.Get(Token.Type.LITERAL)))
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
      Variable? variable = SearchVariable(name);
      if (variable == null)
        Error($"Variable {name} does not exist");
      expression = new IdentifierExpression(variable);
    }
    
    else Error("Expected Expression");
    
    DataType expr_type = expression!.GetReturnType();
    DataType? check_type = typeCheckerContext.Peek();
    
    if (expr_type != check_type)
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
