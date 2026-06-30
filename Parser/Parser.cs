using Lexer;

namespace Parser;

public partial class Parser(List<Token> tokens) : Processor<Token, Statement>(tokens)
{
  protected Stack<ClassDescriptor> current_class = [];
  protected List<ClassDescriptor> classes = [];
  protected Expression ParseExpression()
  {
    throw new NotImplementedException();
  }
  
  protected Statement Parse()
  {
    Statement? statement = null;
    WithModifiers(handler => {
      if (Wakeup(Token.Type.FUN))
      {
        if (handler.IsMutable)
          Error("Function cannot be mutable, only readonly is accepted");
        
        if (current_class.Count < 1)
          Error("Cannot declare a function outside of a class");
        
        string name = ParseIdentifier();

        return;
      }

      else if (Wakeup(Token.Type.CLASS))
      {
        if (handler.IsMutable)
          Error("Class cannot be mutable, only readonly is accepted");

        string name = ParseIdentifier();
        List<Token> body = (List<Token>)TryConsumeError(new Token(Token.Type.CURLY_BLOCK)).value!;

        current_class.Push(new ClassDescriptor(handler, name));

        Switch(body, () => Parse());

        classes.Add(current_class.Pop());

        return;
      }


    });
    if (statement == null)
      Error("Invalid statement");
    return statement;
  }

  public override List<Statement> Process()
  {
    while (HasPeek())
      _ = this << Parse();
    return output;
  }
}
