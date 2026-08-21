using Lexer;

namespace Parser;

public partial class Parser(Token[] tokens) : Processor<Token, Statement>(tokens)
{
  private readonly Stack<string> namespaces = [];
}
