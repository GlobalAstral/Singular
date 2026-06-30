using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace Lexer;

public class Lexer(List<char> content) : Processor<char, Token>(content)
{
  private static char GetCloseBracket(char bracket) => bracket == '(' ? ')' : bracket == '[' ? ']' : bracket == '{' ? '}' : bracket == '<' ? '>' : '\0';
  private static Token.Type GetTokenForBracket(char bracket) => bracket == '(' ? Token.Type.PAREN_BLOCK : bracket == '[' ? Token.Type.SQUARE_BLOCK : bracket == '{' ? Token.Type.CURLY_BLOCK : bracket == '<' ? Token.Type.ANGLE_BLOCK : Token.Type.NULL;
  protected int line = 1;

  private static bool IsCharHexLetter(char c) {
    char ch = char.ToUpper(c);
    return ch == 'A' || ch == 'B' || ch == 'C' || ch == 'D' || ch == 'E' || ch == 'F';
  }
  private Token Tokenize()
  { 
    if (TryConsume('\n'))
    {
      line++;
      return new Token();
    }

    else if (char.IsWhiteSpace(Peek()))
    {
      Consume();
      return new Token();
    }

    else if (Peek('(') || Peek('[') || Peek('{') || Peek('<') && CheckAheadFor('>'))
    {
      char open = Consume();
      List<Token> tokens = [];
      DoUntil(GetCloseBracket(open), () => tokens.Add(Tokenize()));
      return new Token(GetTokenForBracket(open), line, tokens);
    }
    
    else if (TryConsume(','))
      return new Token(Token.Type.COMMA, line);
    
    else if (TryConsume(':'))
      return new Token(Token.Type.COLON, line);
    
    else if (TryConsume(';'))
      return new Token(Token.Type.SEMI, line);

    else if (TryConsume('*'))
      return new Token(Token.Type.STAR, line);
    else if (TryConsume('='))
      return new Token(Token.Type.EQUALS, line);

    else if (char.IsDigit(Peek()))
    {
      bool hex = false;
      StringBuilder builder = new();
      if (Peek('0') && Peek('x', 1))
      {
        Consume(2);
        builder.Append("0x");
        hex = true;
      }

      while (char.IsDigit(Peek()) || (Peek('.') && !hex) || (hex && IsCharHexLetter(Peek())))
        builder.Append(Consume());

      if (Peek('h') || Peek('l') || Peek('f'))
        builder.Append(Consume());
      return new Token(Token.Type.LITERAL, line, builder.ToString());
    }

    else if (char.IsAsciiLetter(Peek()) || Peek('_'))
    {
      StringBuilder builder = new();
      while (char.IsLetterOrDigit(Peek()) || Peek('_'))
        builder.Append(Consume());

      string identifier = builder.ToString();

      return identifier switch
      {
        "public" => new Token(Token.Type.PUBLIC, line),
        "private" => new Token(Token.Type.PRIVATE, line),
        "protected" => new Token(Token.Type.PROTECTED, line),
        "mutable" => new Token(Token.Type.MUTABLE, line),
        "readonly" => new Token(Token.Type.READONLY, line),
        "class" => new Token(Token.Type.CLASS, line),
        "static" => new Token(Token.Type.STATIC, line),
        "return" => new Token(Token.Type.RETURN, line),
        "byte" => new Token(Token.Type.BYTE, line),
        "char" => new Token(Token.Type.CHAR, line),
        "ushort" => new Token(Token.Type.USHORT, line),
        "short" => new Token(Token.Type.SHORT, line),
        "uint" => new Token(Token.Type.UINT, line),
        "int" => new Token(Token.Type.INT, line),
        "ulong" => new Token(Token.Type.ULONG, line),
        "long" => new Token(Token.Type.LONG, line),
        "boolean" => new Token(Token.Type.BOOLEAN, line),
        "float" => new Token(Token.Type.FLOAT, line),
        "double" => new Token(Token.Type.DOUBLE, line),
        "string" => new Token(Token.Type.STRING, line),
        "object" => new Token(Token.Type.OBJECT, line),
        "fun" => new Token(Token.Type.FUN, line), 
        _ => new Token(Token.Type.IDENTIFIER, line, identifier),
      };
    }
    
    Error("Invalid Token");
    return new Token();
  }

  public override List<Token> Process()
  {
    while (HasPeek())
    {
      Token token = Tokenize();
      if (token.type != Token.Type.NULL)
        _ = this << token;
    }
    return output;
  }
}
