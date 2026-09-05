using System.Diagnostics;
using Lexer;

namespace Preprocessor;

public partial class Preprocessor(Token[] tokens) : Processor<Token, Token>(tokens)
{
  protected Context context = new([]);
  public Token[] PreprocessDirective()
  {
    if (TryConsume(Token.Get(Token.Type.EXPORT)))
    {
      Export export = ParseExport(context.Exports);
      context.Exports.Add(export);
    }
    else if (TryConsume(Token.Get(Token.Type.IMPORT)))
    {
      
    }
    else if (TryConsume(Token.Get(Token.Type.INCLUDE)))
    {
      throw new NotImplementedException();
    }

    Token peek = Peek();
    Error($"Invalid directive at (ln: {peek.info.Line}, file: {peek.info.File})");
    throw new UnreachableException();
  }
 
  public override Token ProcessOne() => throw new NotImplementedException();
  public Token[] GetRegular()
  {
    List<Token> ret = [];
    while (!Peek(Token.Get(Token.Type.DOLLAR)))
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
