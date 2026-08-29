using System.Text;

namespace Lexer;

public class Lexer(char[] content) : Processor<char, Token>(content, t => t.type == Token.Type.INVALID)
{
  private static char GetCloseBracket(char bracket) => bracket == '(' ? ')' : bracket == '[' ? ']' : bracket == '{' ? '}' : bracket == '<' ? '>' : '\0';
  private static Token.Type GetTokenForBracket(char bracket) => bracket == '(' ? Token.Type.PAREN_BLOCK : bracket == '[' ? Token.Type.SQUARE_BLOCK : bracket == '{' ? Token.Type.CURLY_BLOCK : bracket == '<' ? Token.Type.ANGLE_BLOCK : Token.Type.INVALID;
  protected int line = 1;

  private static bool IsCharHexLetter(char c) {
    char ch = char.ToUpper(c);
    return ch == 'A' || ch == 'B' || ch == 'C' || ch == 'D' || ch == 'E' || ch == 'F';
  }
  public override Token ProcessOne()
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
      DoUntil(GetCloseBracket(open), () =>
      {
        Token token = ProcessOne();
        if (token.type != Token.Type.INVALID)
          tokens.Add(token);
      });
      return new Token(GetTokenForBracket(open), line, tokens.ToArray());
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
    else if (TryConsume('.'))
      return new Token(Token.Type.DOT, line);
    else if (TryConsume('+'))
      return new Token(Token.Type.PLUS, line);
    else if (TryConsume('-'))
      return new Token(Token.Type.MINUS, line);
    else if (TryConsume('!'))
      return new Token(Token.Type.EXCLAMATION, line);
    else if (TryConsume('~'))
      return new Token(Token.Type.TILDE, line);
    else if (TryConsume('&'))
      return new Token(Token.Type.AMPER, line);
    else if (TryConsume('?'))
      return new Token(Token.Type.QUESTION, line);
    else if (TryConsume('/'))
      return new Token(Token.Type.SLASH, line);
    else if (TryConsume('%'))
      return new Token(Token.Type.PERCENT, line);
    else if (TryConsume('^'))
      return new Token(Token.Type.CARET, line);
    else if (TryConsume('<'))
      return new Token(Token.Type.LANGLE, line);
    else if (TryConsume('>'))
      return new Token(Token.Type.RANGLE, line);
    else if (TryConsume('|'))
      return new Token(Token.Type.PIPE, line);

    else if (TryConsume('\''))
    {
      StringBuilder builder = new();
      builder.Append('\'');
      DoUntil('\'', () => builder.Append(Consume()));
      builder.Append('\'');
      return new Token(Token.Type.LITERAL, line, builder.ToString());
    }

    else if (TryConsume('"'))
    {
      StringBuilder builder = new();
      builder.Append('"');
      DoUntil('"', () => builder.Append(Consume()));
      builder.Append('"');
      return new Token(Token.Type.LITERAL, line, builder.ToString());
    }

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
        "fun" => new Token(Token.Type.FUN, line),
        "namespace" => new(Token.Type.NAMESPACE, line),
        "true" => new(Token.Type.LITERAL, line, "true"),
        "false" => new(Token.Type.LITERAL, line, "false"),
        "null" => new(Token.Type.NULL, line),
        "struct" => new(Token.Type.STRUCT, line),
        "union" => new(Token.Type.UNION, line),
        "var" => new(Token.Type.VAR, line),
        "sizeof" => new(Token.Type.SIZEOF, line),
        "as" => new(Token.Type.AS, line),
        "bitcast" => new(Token.Type.BITCAST, line),
        "dynamic" => new(Token.Type.DYNAMIC, line),
        "type" => new(Token.Type.TYPE, line),
        "mut" => new(Token.Type.MUTABLE, line),
        "defer" => new(Token.Type.DEFER, line),
        "if" => new(Token.Type.IF, line),
        "else" => new(Token.Type.ELSE, line),
        _ => new Token(Token.Type.IDENTIFIER, line, identifier),
      };
    }
    
    Error("Invalid Token");
    return new Token();
  }
}
