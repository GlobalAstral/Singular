using Lexer;

namespace Preprocessor;

public record Export(string Name, bool Once, Token[] Content, List<Export> Exports, uint ID)
{
  private static uint CURRENT_ID = 0;
  public Export(string Name, bool Once, Token[] Content, List<Export> Exports) : this(Name, Once, Content, Exports, CURRENT_ID++) { }
}

public record Context(List<Export> Exports) { }

public partial class Preprocessor
{
  protected Export ParseExport(List<Export> exports)
  {
    bool once = TryConsume(new(Token.Type.LITERAL, (object?)"\"once\""));
    string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
    if (exports.Any(e => e.Name == name))
      Error($"Export {name} already exists");
    Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
    Token[] other = ParseExportsOnly(body, out var found_exports);
    return new Export(name, once, other, found_exports);
  }
  protected Token[] ParseExportsOnly(Token[] body, out List<Export> exports)
  {
    List<Export> temp = [];
    Token[] content = Switch(body, () =>
    {
      List<Token> content = [];
      while (HasPeek())
      {
        if (Peek(Token.Get(Token.Type.DOLLAR)) && Peek(Token.Get(Token.Type.EXPORT), 1))
        {
          Consume(2);
          Export export = ParseExport(temp);
          temp.Add(export);
        }
        if (HasPeek())
          content.Add(Consume());
      }
      return content.ToArray();
    });
    exports = temp;
    return content;
  }
  //TODO
  protected string? SearchImportPath(string file) => null;

  protected Token[] ResolveAllExports(List<Export> all)
  {
    List<Token> result = [];
    foreach (Export ex in all)
      result.AddRange(ResolveExport(ex, true));
    return [.. result];
  }
  protected Token[] ResolveExport(Export export, bool ResolveAll)
  {
    List<Token> result = [];

    if (export.Once && IncludedOnce.Contains(export.ID))
      return [];

    result.AddRange(Switch(export.Content, Process));

    if (ResolveAll)
      result.AddRange(ResolveAllExports(export.Exports));

    if (export.Once)
      IncludedOnce.Add(export.ID);
   
    return [.. result];
  }
  
}
