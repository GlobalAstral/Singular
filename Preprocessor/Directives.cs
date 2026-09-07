
using System.Text;
using Lexer;

namespace Preprocessor;

public record Directive(Token.Type Wakeup, bool Consume, Func<Token[]> Factory) { }

public partial class Preprocessor
{
  protected void RegisterDirectives()
  {
    Directive(Token.Type.EXPORT, true, () => {
      Export export = ParseExport(Exports);
      Exports.Add(export);
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

    Directive(Token.Type.INCLUDE_BYTES, true, () =>
    {
      Token[] tokens = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      if (tokens.Length != 1) Error("Invalid file block");
      string path = Switch(tokens, () => (string) TryConsumeError(Token.Get(Token.Type.LITERAL)).value!);
      if (!path.StartsWith('"') || !path.EndsWith('"')) Error("Expected string literal");
      path = path[1..^1];
      if (!File.Exists(path))
        Error($"File {path} does not exist");
      byte[] content = File.ReadAllBytes(path);
      List<Token> result = [];

      int count = 0;
      foreach (byte b in content)
      {
        if (count++ > 0) result.Add(Token.Get(Token.Type.COMMA)); 
        result.Add(new(Token.Type.LITERAL, tokenInfos.Peek(), $"0b{Convert.ToString(b, 2).PadLeft(8, '0')}"));
      }

      return new(Token.Type.SQUARE_BLOCK, tokenInfos.Peek(), result.ToArray());
    });

    Directive(Token.Type.MACRO, true, () =>
    {
      string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
      if (macros.ContainsKey(name))
        Error($"Macro {name} already exists");
      
      Token[] tokens;
      Macro macro;
      if (Peek(Token.Get(Token.Type.CURLY_BLOCK)))
      {
        tokens = (Token[]) Consume().value!;
        macro = new SimpleMacro(name, [ .. tokens ]);
        macros[name] = macro;
        return [];
      }

      HashSet<string> args = [];
      string? variadic = null;
      tokens = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      Switch(tokens, () =>
      {
        if ( variadic == null && Peek(Token.Get(Token.Type.DOT)) && Peek(Token.Get(Token.Type.DOT), 1) && Peek(Token.Get(Token.Type.DOT), 2) )
        {
          Consume(3);
          variadic = (string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
          if (args.Contains(variadic))
            Error($"Argument {variadic} already exists");
          return;
        }
        string arg = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
        if (args.Contains(arg))
          Error($"Argument {arg} already exists");
        args.Add(arg);
      }, Token.Get(Token.Type.COMMA));

      tokens = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
      macro = new ArgsMacro(name, tokens, [ .. args ], variadic);
      macros[name] = macro;
      return []; 
    });

    Directive(Token.Type.IDENTIFIER, false, () =>
    {
      string name = (string) Consume().value!;

      if (currentMacroArgs.TryGetValue(name, out Token[]? value))
        return value;

      if (!macros.TryGetValue(name, out Macro? macro))
        Error($"Macro {name} does not exist");
      
      if (macro is SimpleMacro simple)
        return Switch(simple.Content, Process);
      
      ArgsMacro argsMacro = (macro as ArgsMacro)!;
      Token[][] args = ParseArgs();
      if (args.Length < argsMacro.Arguments.Length || (argsMacro.Variadic == null && args.Length > argsMacro.Arguments.Length))
        Error($"Invalid {argsMacro.Name} macro arguments. Provided {args.Length} Expected {argsMacro.Arguments.Length}");

      int i = 0;
      for (; i < argsMacro.Arguments.Length; i++)
      {
        string arg = argsMacro.Arguments[i];
        Token[] tokens = args[i];
        currentMacroArgs[arg] = tokens;
      }
      var remainder = args.Skip(i);
      if (argsMacro.Variadic != null && remainder.Any())
      {
        List<Token> tokens = [];
        int count = 0;
        foreach (Token[] toks in remainder)
        {
          if (count++ > 0)
            tokens.Add(Token.Get(Token.Type.COMMA));
          tokens.AddRange(toks);
        }
        currentMacroArgs.Add(argsMacro.Variadic, [.. tokens]);
      }
      return Switch(argsMacro.Content, Process);
    });

    Directive(Token.Type.OPTIONAL, true, () =>
    {
      Token[] temp = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      (string name, Token[] rest) = Switch(temp, () =>
      {
        List<Token> rest = [];
        string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
        TryConsumeError(Token.Get(Token.Type.COMMA));
        while (HasPeek())
          rest.Add(Consume());
        return (name, rest.ToArray());
      });
      if (currentMacroArgs.TryGetValue(name, out var content) && content.Length > 0 || (macros.TryGetValue(name, out var macro) && (macro is SimpleMacro simple ? (simple.Content.Length > 0) : ((macro as ArgsMacro)!.Content.Length > 0))))
        return Switch(rest, Process);
      return [];
    });

    Directive(Token.Type.STRINGIFY, true, () =>
    {
      Token[] processed = Switch((Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!, Process);
      if (processed.Length != 1)
        Error("Expected single token block");
      Token token = processed[0];
      string? str = token.Stringify();
      if (str == null)
        Error($"Cannot stringify token {token}");
      return new Token(Token.Type.LITERAL, tokenInfos.Peek(), (object?) $"\"{str}\"");
    });

    Directive(Token.Type.CONCAT, true, () =>
    {
      Token[][] comma_separated = ParseArgs();
      StringBuilder builder = new();
      foreach (Token[] body in comma_separated)
      {
        Token[] processed = Switch(body, Process);
        if (processed.Length != 1)
          Error($"Each argument of $concat must result in one single token at a time");
        Token token = processed[0];
        builder.Append(token.Stringify());
      }
      return new(Token.Type.IDENTIFIER, tokenInfos.Peek(), builder.ToString());
    });

    Directive(Token.Type.DEL, true, () =>
    {
      string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
      if (!macros.Remove(name))
        Error($"Macro {name} does not exist");
      return [];
    });

    Directive(Token.Type.IF, true, () =>
    {
      dynamic val = PreprocessExpr();
      Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
      if (val)
        return Switch(body, Process);
      if (TryConsume(Token.Get(Token.Type.ELSE)))
      {
        body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
        return Switch(body, Process);
      }
      return [];
    });
  }
}
