
using Lexer;

namespace Preprocessor;

public record Directive(Token.Type Wakeup, bool Consume, Func<Token[]> Factory) { }

public partial class Preprocessor
{
  protected void RegisterDirectives()
  {
    Directive(Token.Type.EXPORT, true, () => {
      Export export = ParseExport(context.Exports);
      context.Exports.Add(export);
      return [];
    });

    Directive(Token.Type.IMPORT, true, () => {
      List<string> names = [];
      do
      {
        string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
        names.Add(name);
      } while (TryConsume(Token.Get(Token.Type.DOT)));
      TryConsumeError(Token.Get(Token.Type.SEMI));
      
      string stem = $"{names[0]}.sgl";
      string? path = File.Exists(stem) ? stem : SearchImportPath(stem);
      if (path == null)
        Error($"File {path} does not exist");

      string content = File.ReadAllText(path);
      Lexer.Lexer lexer = new([.. content], path);
      Token[] body = lexer.Process();

      ParseExportsOnly(body, out var exports);
      if (exports.Count == 0)
        Warn($"File {path} contains no $export directives");
      
      if (names.Count == 1)
        return ResolveAllExports(exports);
      
      string first = names[1];
      Export? export = exports.Find(ex => ex.Name == first);
      if (export == null)
        Error($"Export {first} not found in file {path}");
      
      if (names.Count == 2)
        return ResolveExport(export, true);

      List<Token> result = [];
      result.AddRange(ResolveExport(export, false));

      Export current = export;
      for (int i = 2; i < names.Count; i++)
      {
        string current_name = names[i];
        Export? found = current.Exports.Find(ex => ex.Name == current_name);
        if (found == null)
          Error($"Export {current_name} not found in export {current.Name}");
        result.AddRange(ResolveExport(found, i == names.Count-1));
        current = found;
      }
      return [.. result];
    });

    Directive(Token.Type.CINCLUDE, true, () =>
    {
      bool local = TryConsume(new(Token.Type.LITERAL, (object?)"\"local\""));
      Token[] tokens = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      if (tokens.Length != 1) Error("Invalid .h file block");
      string header = Switch(tokens, () => (string) TryConsumeError(Token.Get(Token.Type.LITERAL)).value!);
      if (!header.StartsWith('"') || !header.EndsWith('"')) Error("Expected string literal");
      header = header[1..^1];
      string raw = local ? $"#include \"{header}\"" : $"#include <{header}>";
      return [new Token(Token.Type.RAWC, tokenInfos.Peek(), raw), Token.Get(Token.Type.SEMI)];
    });

    Directive(Token.Type.INCLUDE_STR, true, () =>
    {
      Token[] tokens = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      if (tokens.Length != 1) Error("Invalid file block");
      string path = Switch(tokens, () => (string) TryConsumeError(Token.Get(Token.Type.LITERAL)).value!);
      if (!path.StartsWith('"') || !path.EndsWith('"')) Error("Expected string literal");
      path = path[1..^1];
      if (!File.Exists(path))
        Error($"File {path} does not exist");
      string content = Escape(File.ReadAllText(path));
      return new Token(Token.Type.LITERAL, tokenInfos.Peek(), $"\"{content}\"");
    });
  }
}
