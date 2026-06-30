
namespace Lexer;

public struct Token(Token.Type type, int line, object? value)
{
  public enum Type {
    NULL,

    PUBLIC, PRIVATE, PROTECTED, MUTABLE, READONLY, CLASS, CURLY_BLOCK, STATIC, PAREN_BLOCK, RETURN, SQUARE_BLOCK, ANGLE_BLOCK,
    COMMA, COLON, SEMI, STAR, EQUALS,
    BYTE, CHAR, USHORT, SHORT, UINT, INT, ULONG, LONG, BOOLEAN, FLOAT, DOUBLE, STRING, OBJECT, FUN,
    LITERAL, IDENTIFIER
  }

  public Type type = type;
  public int line = line;
  public object? value = value;

  public Token() : this(Type.NULL, 0, null) { }
  public Token(Type type, int line) : this(type, line, null) { }
  public Token(Type type) : this(type, -1, null) { }
  public static bool operator ==(Token? a, Token? b) => Equals(a, b);
  public static bool operator !=(Token? a, Token? b) => !Equals(a, b);

  public override readonly bool Equals(object? obj) => obj is Token b && type == b.type && ((value == null && b.value == null) || value == b.value);
  public override readonly int GetHashCode() => HashCode.Combine(type, line, value);

  public override readonly string ToString()
  {
    return $"[{type}] Line {line}: \"{value}\"";
  }
}
