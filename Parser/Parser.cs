using Lexer;

namespace Parser;

using Statement = SyntaxInstance;

public partial class Parser(List<Token> tokens) : Processor<Token, Statement>(tokens)
{
  public Token Require(Token consume) => TryConsumeError(consume);
  public bool Wakeup(Token consume) => TryConsume(consume);

  private Statement Parse()
  {
    foreach (Syntax syntax in syntaxes)
    {
      Statement? instance = syntax.Build(this);
      if (instance == null)
        continue;
      return instance;
    }
    Error("Syntax Error");
    return new();
  }

  public override List<Statement> Process()
  {
    RegisterSyntaxes();

    while (HasPeek())
    {
      Statement statement = Parse();
      if (statement.Id == NodeInstanceID.NULL)
        continue;
      _ = this << statement;
    }
    
    return output;
  }
}
