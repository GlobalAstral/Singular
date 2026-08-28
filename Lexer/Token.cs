
namespace Lexer;

public struct Token(Token.Type type, int line, object? value)
{
  public enum Type {
    INVALID,
    NULL,
    MUTABLE, CURLY_BLOCK, STATIC, PAREN_BLOCK, RETURN, SQUARE_BLOCK, ANGLE_BLOCK, NAMESPACE, VAR, TYPE, DEFER,
    COMMA, COLON, SEMI, STAR, EQUALS, DOT, AS, BITCAST, SLASH, PERCENT, PIPE, CARET, LANGLE, RANGLE,
    PLUS, MINUS, EXCLAMATION, TILDE, AMPER, SIZEOF, QUESTION,
    STRUCT, UNION,
    BYTE, CHAR, USHORT, SHORT, UINT, INT, ULONG, LONG, BOOLEAN, FLOAT, DOUBLE, STRING, FUN, DYNAMIC,
    LITERAL, IDENTIFIER
  }

  private static readonly Dictionary<Type, Token> INSTANCES = new()
  {
    [Type.INVALID] = new(Type.INVALID),
    [Type.NULL] = new(Type.NULL),
    [Type.MUTABLE] = new(Type.MUTABLE),
    [Type.CURLY_BLOCK] = new(Type.CURLY_BLOCK),
    [Type.STATIC] = new(Type.STATIC),
    [Type.PAREN_BLOCK] = new(Type.PAREN_BLOCK),
    [Type.RETURN] = new(Type.RETURN),
    [Type.SQUARE_BLOCK] = new(Type.SQUARE_BLOCK),
    [Type.ANGLE_BLOCK] = new(Type.ANGLE_BLOCK),
    [Type.NAMESPACE] = new(Type.NAMESPACE),
    [Type.COMMA] = new(Type.COMMA),
    [Type.COLON] = new(Type.COLON),
    [Type.SEMI] = new(Type.SEMI),
    [Type.STAR] = new(Type.STAR),
    [Type.EQUALS] = new(Type.EQUALS),
    [Type.BYTE] = new(Type.BYTE),
    [Type.CHAR] = new(Type.CHAR),
    [Type.USHORT] = new(Type.USHORT),
    [Type.SHORT] = new(Type.SHORT),
    [Type.UINT] = new(Type.UINT),
    [Type.INT] = new(Type.INT),
    [Type.ULONG] = new(Type.ULONG),
    [Type.LONG] = new(Type.LONG),
    [Type.BOOLEAN] = new(Type.BOOLEAN),
    [Type.FLOAT] = new(Type.FLOAT),
    [Type.DOUBLE] = new(Type.DOUBLE),
    [Type.STRING] = new(Type.STRING),
    [Type.FUN] = new(Type.FUN),
    [Type.LITERAL] = new(Type.LITERAL),
    [Type.IDENTIFIER] = new(Type.IDENTIFIER),
    [Type.STRUCT] = new(Type.STRUCT),
    [Type.UNION] = new(Type.UNION),
    [Type.VAR] = new(Type.VAR),
    [Type.DOT] = new(Type.DOT),
    [Type.PLUS] = new(Type.PLUS),
    [Type.MINUS] = new(Type.MINUS),
    [Type.EXCLAMATION] = new(Type.EXCLAMATION),
    [Type.TILDE] = new(Type.TILDE),
    [Type.AMPER] = new(Type.AMPER),
    [Type.SIZEOF] = new(Type.SIZEOF),
    [Type.AS] = new(Type.AS),
    [Type.BITCAST] = new(Type.BITCAST),
    [Type.QUESTION] = new(Type.QUESTION),
    [Type.DYNAMIC] = new(Type.DYNAMIC),
    [Type.TYPE] = new(Type.TYPE),
    [Type.SLASH] = new(Type.SLASH),
    [Type.PERCENT] = new(Type.PERCENT),
    [Type.PIPE] = new(Type.PIPE),
    [Type.CARET] = new(Type.CARET),
    [Type.LANGLE] = new(Type.LANGLE),
    [Type.RANGLE] = new(Type.RANGLE),
    [Type.DEFER] = new(Type.DEFER),
  };

  public static Token Get(Type type) => INSTANCES[type];

  public Type type = type;
  public int line = line;
  public object? value = value;

  public Token() : this(Type.INVALID, 0, null) { }
  public Token(Type type, int line) : this(type, line, null) { }
  public Token(Type type) : this(type, -1, null) { }
  public static bool operator ==(Token? a, Token? b) => Equals(a, b);
  public static bool operator !=(Token? a, Token? b) => !Equals(a, b);

  public override readonly bool Equals(object? obj) => obj is Token b && type == b.type && (value == null || b.value == null || value == b.value);
  public override readonly int GetHashCode() => HashCode.Combine(type, line, value);

  public override readonly string ToString()
  {
    return $"[{type}] Line {line}: \"{value}\"";
  }
}
