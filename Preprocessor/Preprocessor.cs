using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Lexer;

namespace Preprocessor;

public partial class Preprocessor : Processor<Token, Token>
{
  public Preprocessor(Token[] tokens) : base(tokens) {
    RegisterDirectives();  
  }

  protected Context context = new([]);
  protected readonly HashSet<uint> IncludedOnce = [];
  protected readonly List<Directive> directives = [];
  protected readonly Stack<TokenInfo> tokenInfos = [];

  [DoesNotReturn]
  protected override void Error(string msg)
  {
    TokenInfo? info = tokenInfos.Count == 0 ? null : tokenInfos.Peek();
    if (info == null)
      base.Error(msg);
    base.Error($"{msg} at (ln: {info.Line}, file: {info.File})");
  }
  protected override void Warn(string msg)
  {
    TokenInfo? info = tokenInfos.Count == 0 ? null : tokenInfos.Peek();
    if (info == null) {
      base.Warn(msg);
      return;
    }
    base.Warn($"{msg} at (ln: {info.Line}, file: {info.File})");
  }
  protected static string EXPECTED_ERROR(Token expected, Token found, TokenInfo info) => $"Error: {EXPECTED_ERROR(expected, found)} at (ln: {info.Line}, file: {info.File})";
  protected override Token TryConsumeError(Token consume)
  {
    if (Peek().Equals(consume))
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

  protected void Directive(Token.Type wakeup, bool consume, Func<Token[]> factory) => directives.Add(new(wakeup, consume, factory));
  protected void Directive(Token.Type wakeup, bool consume, Func<Token> factory) => directives.Add(new(wakeup, consume, () => [factory()]));
  public Token[] PreprocessDirective()
  {
    (Directive dir, TokenInfo info)? result = null;

    foreach (Directive directive in directives)
    {
      Token tok = Token.Get(directive.Wakeup);
      if (Peek(tok))
      {
        Token p = Peek();
        result = (directive, p.info);
        if (directive.Consume)
          Consume();
        break;
      }
    }

    if (result.HasValue)
    {
      tokenInfos.Push(result.Value.info);
      Token[] ret = result.Value.dir.Factory();
      tokenInfos.Pop();
      return ret;
    }

    Token peek = Peek();
    base.Error($"Invalid directive at (ln: {peek.info.Line}, file: {peek.info.File})");
    throw new UnreachableException();
  }
 
  public override Token ProcessOne() => throw new NotImplementedException();
  public Token[] GetRegular()
  {
    List<Token> ret = [];
    while (HasPeek() && !Peek(Token.Get(Token.Type.DOLLAR)))
      ret.Add(Consume());
    return [.. ret];
  }
  public Token[] PreprocessOne()
  {
    if (TryConsume(Token.Get(Token.Type.DOLLAR)))
      return PreprocessDirective();
    return GetRegular();
  }
  public override Token[] Process()
  {
    List<Token> ret = [];
    while (HasPeek())
      ret.AddRange(PreprocessOne());
    return [.. ret];
  }
}
