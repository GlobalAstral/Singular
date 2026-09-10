using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Lexer;
using Tomlyn.Model;

namespace Preprocessor;

public partial class Preprocessor : Processor<Token, Token>
{
  public Preprocessor(Token[] tokens, string[] importPath, TomlTable platform) : base(tokens) {
    this.importPath = importPath;
    InitMacros(platform);
    VerifyImportPath();
    RegisterDirectives();
  }

  protected readonly List<Export> Exports = [];
  protected readonly HashSet<int> IncludedOnce = [];
  protected readonly List<Directive> directives = [];
  protected readonly Stack<TokenInfo> tokenInfos = [];
  protected readonly string[] importPath;
  protected readonly Dictionary<string, Macro> macros = [];
  protected readonly Dictionary<string, Token[]> currentMacroArgs = [];
  protected readonly HashSet<string> GenericBlocks = [];
  public List<Token> Output() => output;

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

  protected void Directive(Token.Type wakeup, bool consume, Func<Token[]> factory) => directives.Add(new(wakeup, consume, o => factory()));
  protected void Directive(Token.Type wakeup, bool consume, Func<Token> factory) => directives.Add(new(wakeup, consume, o => [factory()]));
  protected void Directive(Token.Type wakeup, bool consume, Func<List<Token>, Token[]> factory) => directives.Add(new(wakeup, consume, factory));
  protected void Directive(Token.Type wakeup, bool consume, Func<List<Token>, Token> factory) => directives.Add(new(wakeup, consume, o => [factory(o)]));

  public Token[] PreprocessDirective(List<Token> output)
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
      Token[] ret = result.Value.dir.Factory(output);
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
    if (Peek(Token.Get(Token.Type.PAREN_BLOCK)))
      return [new Token(Token.Type.PAREN_BLOCK, Peek().info, Switch((Token[]) Consume().value!, Process))];
    if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
      return [new Token(Token.Type.SQUARE_BLOCK, Peek().info, Switch((Token[]) Consume().value!, Process))];
    if (Peek(Token.Get(Token.Type.CURLY_BLOCK)))
      return [new Token(Token.Type.CURLY_BLOCK, Peek().info, Switch((Token[]) Consume().value!, Process))];
    if (Peek(Token.Get(Token.Type.ANGLE_BLOCK)))
      return [new Token(Token.Type.ANGLE_BLOCK, Peek().info, Switch((Token[]) Consume().value!, Process))];

    List<Token> ret = [];
    while (HasPeek() && !Peek(Token.Get(Token.Type.DOLLAR)) && !Peek(Token.Get(Token.Type.PAREN_BLOCK)) && !Peek(Token.Get(Token.Type.SQUARE_BLOCK)) && !Peek(Token.Get(Token.Type.CURLY_BLOCK)) && !Peek(Token.Get(Token.Type.ANGLE_BLOCK)))
      ret.Add(Consume());
    return [.. ret];
  }
  public Token[] PreprocessOne(List<Token> output)
  {
    if (TryConsume(Token.Get(Token.Type.DOLLAR)))
      return PreprocessDirective(output);
    return GetRegular();
  }
  public override Token[] Process()
  {
    List<Token> ret = [];
    while (HasPeek())
      ret.AddRange(PreprocessOne(ret));
    return [.. ret];
  }
}
