using System.Collections;
using System.Diagnostics;
using Lexer;

namespace Parser;

public partial class Parser : Processor<Token, Statement>
{
  public Parser(Token[] tokens) : base(tokens, s => s is Nop) => Init();
  private readonly Stack<string> namespaces = [];
  private readonly List<Function> functions = [];
  private readonly Stack<Context> currentContext = [];
  private readonly Stack<ScopeContext> activeScopes = [];
  private readonly Stack<DataType?> typeCheckerContext = [];
  private readonly List<Variable> globals = [];
  private readonly Dictionary<string, Composite> composites = [];
  private readonly Dictionary<string, DataType> aliases = [];
  private uint IgnoringExpression = 0;
  private bool extendedExpr = true;
  private readonly List<ParsingProcess> processes = [];

  public override Statement ProcessOne()
  {
    ParsingProcess? process = processes.Find(process =>
    {
      Token tok = Token.Get(process.Type);
      return (process.Consume && TryConsume(tok)) || (!process.Consume && Peek(tok));
    });

    Statement? ret = process?.Factory(); 
    
    if (ret == null)
    {
      IgnoringExpression++;
      Expression expression = ParseExpression(null);
      IgnoringExpression--;
      Semi();
      ret = new IgnoredExpr(expression);
    }
    return ret;
  }
}
