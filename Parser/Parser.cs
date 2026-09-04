using System.Diagnostics.CodeAnalysis;
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
  private readonly Stack<TokenInfo> tokenInfos = [];

  [DoesNotReturn]
  protected override void Error(string msg)
  {
    TokenInfo? info = tokenInfos.Count == 0 ? null : tokenInfos.Peek();
    if (info == null)
      base.Error(msg);
    base.Error($"{msg} at (ln: {info.Line}, file: {info.File})");
  }

  protected static string EXPECTED_ERROR(Token expected, Token found, TokenInfo info) => $"Error: {EXPECTED_ERROR(expected, found)} at (ln: {info.Line}, file: {info.File})";

  protected override Token TryConsumeError(Token consume)
  {
    if (EqualityComparer<Token>.Default.Equals(Peek(), consume))
      return Consume();

    Token token = Peek();
    Error(EXPECTED_ERROR(consume, token, token.info));
    return new Token();
  }

  protected override void DoUntil(Token find, Action action)
  {
    bool found = false;
    Token instead = default;
    while (HasPeek())
    {
      if (TryConsume(find))
      {
        instead = default;
        found = true;
        break; 
      }
      instead = Peek();
      action();
    }
    if (!found)
      Error(EXPECTED_ERROR(find, instead, instead.info!));
  }

  public override Statement ProcessOne()
  {
    (ParsingProcess process, TokenInfo info)? result = null;

    foreach (ParsingProcess process in processes)
    {
      Token tok = Token.Get(process.Type);
      if (Peek(tok))
      {
        Token p = Peek();
        result = (process, p.info);
        if (process.Consume)
          Consume();
        break;
      }
    }

    Statement? Create()
    {
      if (result.HasValue)
      {
        tokenInfos.Push(result.Value.info);
        Statement? ret = result.Value.process.Factory(result.Value.info);
        tokenInfos.Pop();
        return ret;
      }
      return null;
    }

    Statement? ret = Create(); 
    
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
