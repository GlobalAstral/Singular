using Lexer;

namespace Parser;

public class ModifierHandler
{
  public bool IsPublic = false;
  public bool IsProtected = false;
  public bool IsPrivate = false;
  public bool IsStatic = false;
  public bool IsMutable = false;
  public bool IsReadonly = false;

  public ModifierHandler Public() {
    IsPublic = true;
    return this;
  }

  public ModifierHandler Protected() {
    IsProtected = true;
    return this;
  }

  public ModifierHandler Private() {
    IsPrivate = true;
    return this;
  }

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

    if (IsPublic != other.IsPublic)
      return false;
    
    if (IsProtected != other.IsProtected)
      return false;
    
    if (IsPrivate != other.IsPrivate)
      return false;
    
    if (IsStatic != other.IsStatic)
      return false;
    
    if (IsMutable != other.IsMutable)
      return false;
    
    if (IsReadonly != other.IsReadonly)
      return false;
    
    return true;
  }

  public override int GetHashCode() => HashCode.Combine(IsPublic, IsProtected, IsPrivate, IsStatic, IsMutable, IsReadonly);

  public static bool operator ==(ModifierHandler a, ModifierHandler b) => a.Equals(b);
  public static bool operator !=(ModifierHandler a, ModifierHandler b) => !a.Equals(b);
}

public partial class Parser
{
  protected T WithModifiers<T>(Func<ModifierHandler, T> action)
  {
    ModifierHandler handler = new();

    if (TryConsume(new Token(Token.Type.PUBLIC)))
      handler.Public();
    else if (TryConsume(new Token(Token.Type.PROTECTED)))
      handler.Protected();
    else if (TryConsume(new Token(Token.Type.PRIVATE)))
      handler.Private();
    
    if (TryConsume(new Token(Token.Type.STATIC)))
      handler.Static();
    
    if (TryConsume(new Token(Token.Type.MUTABLE)))
      handler.Mutable();
    
    if (TryConsume(new Token(Token.Type.READONLY)))
      handler.Readonly();
    
    return action(handler);
  }

  protected void WithModifiers(Action<ModifierHandler> action) => WithModifiers(handler =>
  {
    action(handler);
    return 0;
  });

  protected DataType ParseType()
  {
    DataType? dataType = null;
    if (TryConsume(new Token(Token.Type.STAR)))
      dataType = References.GetPointerType(ParseType());
    if (TryConsume(new Token(Token.Type.BYTE)))
      dataType = ByteType.INSTANCE;
    if (TryConsume(new Token(Token.Type.CHAR)))
      dataType = CharType.INSTANCE;
    if (TryConsume(new Token(Token.Type.USHORT)))
      dataType = UShortType.INSTANCE;
    if (TryConsume(new Token(Token.Type.SHORT)))
      dataType = ShortType.INSTANCE;
    if (TryConsume(new Token(Token.Type.UINT)))
      dataType = UIntType.INSTANCE;
    if (TryConsume(new Token(Token.Type.INT)))
      dataType = IntType.INSTANCE;
    if (TryConsume(new Token(Token.Type.ULONG)))
      dataType = ULongType.INSTANCE;
    if (TryConsume(new Token(Token.Type.LONG)))
      dataType = LongType.INSTANCE;
    if (TryConsume(new Token(Token.Type.BOOLEAN)))
      dataType = BooleanType.INSTANCE;
    if (TryConsume(new Token(Token.Type.FLOAT)))
      dataType = FloatType.INSTANCE;
    if (TryConsume(new Token(Token.Type.DOUBLE)))
      dataType = DoubleType.INSTANCE;
    if (TryConsume(new Token(Token.Type.FUN)))
    {
      DataType[] args = [.. ParseArgs().Select(v => v.Type)];
      DataType? result = null;
      if (TryConsume(new Token(Token.Type.COLON)))
        result = ParseType();
      return References.GetFunctionType(result, args);
    }
    
    if (dataType == null)
      Error("Expected Type");
    
    if (Peek(new Token(Token.Type.SQUARE_BLOCK)))
    {
      List<Token> temp = (List<Token>)Consume().value!;
      if (temp.Count != 0)
        Error("Invalid Array Type");
      dataType = References.GetArrayType(dataType);
    }
    return dataType;
  }

  protected List<Variable> ParseArgs()
  {
    List<Token> args = (List<Token>)TryConsumeError(new Token(Token.Type.PAREN_BLOCK)).value!;
    List<Variable> arguments = [];
    Switch(args, () =>
    {
      DataType t = ParseType();
      string ident = ParseIdentifier();
      if (arguments.Any(v => v.Name == ident))
        Error($"Function type cannot have duplicate arguments");
      arguments.Add(new Variable(t, ident));
    }, new Token(Token.Type.COMMA));
    return arguments;
  }

  public void SavePeek() => saved_peek = peek;
  public void RestorePeek() => peek = saved_peek;

  private int saved_peek = 0;

  private static readonly Token IDENTIFIER = new(Token.Type.IDENTIFIER);
  private static readonly Token SEMI = new(Token.Type.SEMI);
  private static readonly Token COMMA = new(Token.Type.COMMA);
  private static readonly Token ANGLE_BLOCK = new(Token.Type.ANGLE_BLOCK);

  protected bool Wakeup(Token.Type token) => TryConsume(new Token(token));
  protected string ParseIdentifier() => (string)TryConsumeError(IDENTIFIER).value!;
  protected void Semi() => TryConsumeError(SEMI);
}
