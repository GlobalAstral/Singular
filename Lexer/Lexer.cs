using System.Text;

namespace Lexer;

public class Lexer(char[] content, string file) : Processor<char, Token>(content, t => t.type == Token.Type.INVALID)
{
  private static char GetCloseBracket(char bracket) => bracket == '(' ? ')' : bracket == '[' ? ']' : bracket == '{' ? '}' : bracket == '<' ? '>' : '\0';
  private static Token.Type GetTokenForBracket(char bracket) => bracket == '(' ? Token.Type.PAREN_BLOCK : bracket == '[' ? Token.Type.SQUARE_BLOCK : bracket == '{' ? Token.Type.CURLY_BLOCK : bracket == '<' ? Token.Type.ANGLE_BLOCK : Token.Type.INVALID;
  protected int line = 1;
  //TODO ADD COMMENTS
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
      return new Token(GetTokenForBracket(open), line, file, tokens.ToArray());
    }
    
    else if (TryConsume(','))
      return new Token(Token.Type.COMMA, line, file);
    
    else if (TryConsume(':'))
      return new Token(Token.Type.COLON, line, file);
    
    else if (TryConsume(';'))
      return new Token(Token.Type.SEMI, line, file);

    else if (TryConsume('*'))
      return new Token(Token.Type.STAR, line, file);
    else if (TryConsume('='))
      return new Token(Token.Type.EQUALS_SYMBOL, line, file);
    else if (TryConsume('.'))
      return new Token(Token.Type.DOT, line, file);
    else if (TryConsume('+'))
      return new Token(Token.Type.PLUS, line, file);
    else if (TryConsume('-'))
      return new Token(Token.Type.MINUS, line, file);
    else if (TryConsume('!'))
      return new Token(Token.Type.EXCLAMATION, line, file);
    else if (TryConsume('~'))
      return new Token(Token.Type.TILDE, line, file);
    else if (TryConsume('&'))
      return new Token(Token.Type.AMPER, line, file);
    else if (TryConsume('?'))
      return new Token(Token.Type.QUESTION, line, file);
    else if (TryConsume('/'))
      return new Token(Token.Type.SLASH, line, file);
    else if (TryConsume('%'))
      return new Token(Token.Type.PERCENT, line, file);
    else if (TryConsume('^'))
      return new Token(Token.Type.CARET, line, file);
    else if (TryConsume('<'))
      return new Token(Token.Type.LANGLE, line, file);
    else if (TryConsume('>'))
      return new Token(Token.Type.RANGLE, line, file);
    else if (TryConsume('|'))
      return new Token(Token.Type.PIPE, line, file);
    else if (TryConsume('$'))
      return new Token(Token.Type.DOLLAR, line, file);

    else if (TryConsume('\''))
    {
      StringBuilder builder = new();
      builder.Append('\'');
      DoUntil('\'', () => builder.Append(Consume()));
      builder.Append('\'');
      return new Token(Token.Type.LITERAL, line, file, builder.ToString());
    }

    else if (Peek('"'))
    {
      StringBuilder builder = new();
      builder.Append('"');
      while (TryConsume('"'))
      {
        DoUntil('"', () => builder.Append(Consume()));
        ProcessWhiteSpace();
      }
      builder.Append('"');
      return new Token(Token.Type.LITERAL, line, file, builder.ToString());
    }

    else if (char.IsDigit(Peek()))
    {
      bool hex = false;
      bool bin = false;
      StringBuilder builder = new();
      if (Peek('0') && Peek('b', 1))
      {
        Consume(2);
        builder.Append("0b");
        bin = true;
      }
      if (Peek('0') && Peek('x', 1))
      {
        Consume(2);
        builder.Append("0x");
        hex = true;
      }

      while (char.IsDigit(Peek()) || (Peek('.') && !hex && !bin) || (hex && IsCharHexLetter(Peek())))
        builder.Append(Consume());

      if (Peek('u'))
        builder.Append(Consume());

      if (Peek('h') || Peek('l') || Peek('f'))
        builder.Append(Consume());
      return new Token(Token.Type.LITERAL, line, file, builder.ToString());
    }

    else if (char.IsAsciiLetter(Peek()) || Peek('_'))
    {
      StringBuilder builder = new();
      while (char.IsLetterOrDigit(Peek()) || Peek('_'))
        builder.Append(Consume());

      string identifier = builder.ToString();

      return identifier switch
      {
        "static" => new Token(Token.Type.STATIC, line, file),
        "return" => new Token(Token.Type.RETURN, line, file),
        "byte" => new Token(Token.Type.BYTE, line, file),
        "char" => new Token(Token.Type.CHAR, line, file),
        "ushort" => new Token(Token.Type.USHORT, line, file),
        "short" => new Token(Token.Type.SHORT, line, file),
        "uint" => new Token(Token.Type.UINT, line, file),
        "int" => new Token(Token.Type.INT, line, file),
        "ulong" => new Token(Token.Type.ULONG, line, file),
        "long" => new Token(Token.Type.LONG, line, file),
        "boolean" => new Token(Token.Type.BOOLEAN, line, file),
        "float" => new Token(Token.Type.FLOAT, line, file),
        "double" => new Token(Token.Type.DOUBLE, line, file),
        "string" => new Token(Token.Type.STRING, line, file),
        "fun" => new Token(Token.Type.FUN, line, file),
        "namespace" => new(Token.Type.NAMESPACE, line, file),
        "true" => new(Token.Type.LITERAL, line, file, "true"),
        "false" => new(Token.Type.LITERAL, line, file, "false"),
        "null" => new(Token.Type.NULL, line, file),
        "struct" => new(Token.Type.STRUCT, line, file),
        "union" => new(Token.Type.UNION, line, file),
        "var" => new(Token.Type.VAR, line, file),
        "sizeof" => new(Token.Type.SIZEOF, line, file),
        "as" => new(Token.Type.AS, line, file),
        "bitcast" => new(Token.Type.BITCAST, line, file),
        "dynamic" => new(Token.Type.DYNAMIC, line, file),
        "type" => new(Token.Type.TYPE, line, file),
        "mut" => new(Token.Type.MUTABLE, line, file),
        "defer" => new(Token.Type.DEFER, line, file),
        "if" => new(Token.Type.IF, line, file),
        "else" => new(Token.Type.ELSE, line, file),
        "infer" => new(Token.Type.INFER, line, file),
        "while" => new(Token.Type.WHILE, line, file),
        "do" => new(Token.Type.DO, line, file),
        "loop" => new(Token.Type.LOOP, line, file),
        "for" => new(Token.Type.FOR, line, file),
        "in" => new(Token.Type.IN, line, file),
        "break" => new(Token.Type.BREAK, line, file),
        "continue" => new(Token.Type.CONTINUE, line, file),
        "switch" => new(Token.Type.SWITCH, line, file),
        "case" => new(Token.Type.CASE, line, file),
        "default" => new(Token.Type.DEFAULT, line, file),
        "rawc" => ParseRawC(),
        "extern" => new(Token.Type.EXTERN, line, file),
        "enum" => new(Token.Type.ENUM, line, file),
        "export" => new(Token.Type.EXPORT, line, file),
        "import" => new(Token.Type.IMPORT, line, file),
        "cinclude" => new(Token.Type.CINCLUDE, line, file),
        "include_str" => new(Token.Type.INCLUDE_STR, line, file),
        "include_bytes" => new(Token.Type.INCLUDE_BYTES, line, file),
        "macro" => new(Token.Type.MACRO, line, file),
        "stringify" => new(Token.Type.STRINGIFY, line, file),
        "concat" => new(Token.Type.CONCAT, line, file),
        "del" => new(Token.Type.DEL, line, file),
        "defined" => new(Token.Type.DEFINED, line, file),
        "equ" => new(Token.Type.EQUALS, line, file),
        "gre" => new(Token.Type.GREATER, line, file),
        "les" => new(Token.Type.LESS, line, file),
        "grq" => new(Token.Type.GREATER_EQUALS, line, file),
        "leq" => new(Token.Type.LESS_EQUALS, line, file),
        _ => new Token(Token.Type.IDENTIFIER, line, file, identifier),
      };
    }
    
    Error("Invalid Token");
    return new Token();
  }

  private void ProcessWhiteSpace()
  {
    while (char.IsWhiteSpace(Peek()))
    {
      if (Peek('\n'))
      {
        line++;
        Consume();
        continue;
      }
      Consume();
    }
  }

  private Token ParseRawC()
  {
    ProcessWhiteSpace();
    TryConsumeError('{');
    StringBuilder code = new();
    DoUntil('}', () => code.Append(Consume()));
    return new(Token.Type.RAWC, line, file, code.ToString().Trim());
  }
}
