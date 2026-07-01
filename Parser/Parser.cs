using Lexer;

namespace Parser;

public partial class Parser(List<Token> tokens) : Processor<Token, Statement>(tokens)
{
  public Token Require(Token consume) => TryConsumeError(consume);
  public bool Wakeup(Token consume) => TryConsume(consume);
  public override List<Statement> Process()
  {
    RegisterSyntaxes();
    
    return output;
  }
}
