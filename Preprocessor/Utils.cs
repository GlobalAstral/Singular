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

  protected void VerifyImportPath()
  {
    foreach (string path in importPath)
    {
      if (!Directory.Exists(path))
        Error($"Directory {path} in include path does not exist");
    }
  }
  protected string? SearchImportPath(string file)
  {
    string oldDir = Environment.CurrentDirectory;

    foreach (string path in importPath)
    {
      Environment.CurrentDirectory = path;
      if (File.Exists(file))
      {
        string ret = Path.GetFullPath(file);
        Environment.CurrentDirectory = oldDir;
        return ret;
      }
    }
    Environment.CurrentDirectory = oldDir;
    return null;
  }

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
  
  static string Escape(string str) => str
    .Replace("\\", "\\\\")
    .Replace("\n", "\\n")
    .Replace("\r", "\\r")
    .Replace("\t", "\\t")
    .Replace("\0", "\\0")
    .Replace("\b", "\\b")
    .Replace("\f", "\\f")
    .Replace("\v", "\\v")
    .Replace("\"", "\\\"");


  Token[][] ParseArgs()
  {
    Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
    Token[][] result = Switch(body, () =>
    {
      List<Token[]> result = [];
      List<Token> current = [];
      while (HasPeek())
      {
        if (TryConsume(Token.Get(Token.Type.COMMA)))
        {
          result.Add([.. current]);
          current.Clear();
          continue;
        }
        current.Add(Consume());
      }
      if (current.Count != 0)
        result.Add([.. current]);
      return result.ToArray();
    });
    return result;
  }

}

public record Macro { }
public record SimpleMacro(string Name, Token[] Content) : Macro { }
public record ArgsMacro(string Name, Token[] Content, string[] Arguments, string? Variadic) : Macro { }
