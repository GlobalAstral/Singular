using System.Diagnostics;
using Lexer;
using Parser;
using Tomlyn.Model;

namespace Preprocessor;

public class Export
{
  public string Name { get; init; }
  public bool Once { get; init; }
  public Token[] Content { get; init; }
  public List<Export> Exports { get; init; }
  public string ID { get; init; }

  private Export(string name, bool once, Token[] content, List<Export> exports, string id)
  {
    Name = name;
    Once = once;
    Content = content;
    Exports = exports;
    ID = id;
  }

  private static readonly Dictionary<string, Export> CachedExports = [];
  public static string GetID(string path, string name) => $"{path}.{name}";
  public static Export FromPath(string Name, bool Once, Token[] Content, List<Export> Exports, string path) => new(Name, Once, Content, Exports, GetID(path, Name));
  public static Export Create(string name, string path, Func<(bool once, Token[] content, List<Export> exports)> factory)
  {
    string id = GetID(path, name);
    if (!CachedExports.TryGetValue(id, out var value))
    {
      (bool once, Token[] content, List<Export> exports) = factory();
      value = FromPath(name, once, content, exports, path);
      CachedExports[id] = value;
    }
    return value;
  }
}

public partial class Preprocessor
{
  protected readonly Dictionary<string, (string path, string content)> builtins = new()
  {
    ["std"] = ResourceHelper.ExtractSglWithPath("builtins.std"),
  };
  protected Export ParseExport(List<Export> exports, string path)
  {
    bool once = TryConsume(new(Token.Type.LITERAL, (object?)"\"once\""));
    string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
    if (exports.Any(e => e.Name == name))
      Error($"Export {name} already exists");
    return Export.Create(name, path, () =>
    {
      Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
      Token[] other = ParseExportsOnly(body, out var found_exports, $"{path}.{name}");
      return (once, other, found_exports);
    });
  }
  protected Token[] ParseExportsOnly(Token[] body, out List<Export> exports, string path)
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
          Export export = ParseExport(temp, path);
          temp.Add(export);
          continue;
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
  protected (string path, string content)? SearchImportPath(string file)
  {
    if (builtins.TryGetValue(file[..^4], out var value))
      return value;

    string oldDir = Environment.CurrentDirectory;

    foreach (string path in importPath)
    {
      Environment.CurrentDirectory = path;
      if (File.Exists(file))
      {
        string ret = Path.GetFullPath(file);
        Environment.CurrentDirectory = oldDir;
        return (ret, File.ReadAllText(ret));
      }
    }
    Environment.CurrentDirectory = oldDir;

    if (File.Exists(file))
      return (file, File.ReadAllText(file));

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


  protected Token[][] ParseArgs(Token.Type block = Token.Type.PAREN_BLOCK)
  {
    Token[] body = (Token[]) TryConsumeError(Token.Get(block)).value!;
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

  protected Func<dynamic, dynamic, bool>? GetOperator()
  {
    if (Peek(Token.Get(Token.Type.EQUALS_SYMBOL)) && Peek(Token.Get(Token.Type.EQUALS_SYMBOL), 1))
    {
      Consume(2);
      return (a, b) => a == b;
    }

    if (Peek(Token.Get(Token.Type.EXCLAMATION)) && Peek(Token.Get(Token.Type.EQUALS_SYMBOL), 1))
    {
      Consume(2);
      return (a, b) => a != b;
    }

    if (TryConsume(Token.Get(Token.Type.RANGLE)))
    {
      if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
        return (a, b) => a >= b;
      return (a, b) => a > b;
    }

    if (TryConsume(Token.Get(Token.Type.LANGLE)))
    {
      if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
        return (a, b) => a <= b;
      return (a, b) => a < b;
    }
    return null;
  }

  protected dynamic PreprocessExpr(List<Token> output)
    => ParseOr(output);

  protected dynamic ParseOr(List<Token> output)
  {
    dynamic left = ParseAnd(output);

    while (Peek(Token.Get(Token.Type.PIPE)) && Peek(Token.Get(Token.Type.PIPE), 1))
    {
      Consume(2);
      dynamic right = ParseAnd(output);
      left = (bool)left || (bool)right;
    }

    return left;
  }

  protected dynamic ParseAnd(List<Token> output)
  {
    dynamic left = ParseComparison(output);

    while (Peek(Token.Get(Token.Type.AMPER)) && Peek(Token.Get(Token.Type.AMPER), 1))
    {
      Consume(2);
      dynamic right = ParseComparison(output);
      left = (bool)left && (bool)right;
    }

    return left;
  }

  protected dynamic ParseComparison(List<Token> output)
  {
    dynamic left = ParseUnaryExpr(output);

    Func<dynamic, dynamic, bool>? op = GetOperator();

    if (op == null)
      return left;

    dynamic right = ParseUnaryExpr(output);

    return op(left, right);
  }

  protected dynamic ParseUnaryExpr(List<Token> output)
  {
    if (TryConsume(Token.Get(Token.Type.EXCLAMATION)))
      return !(bool)ParseUnaryExpr(output);
    return PreprocessExpr__(output);
  }

  protected dynamic PreprocessExpr__(List<Token> output)
  {
    if (Peek(Token.Get(Token.Type.LITERAL)))
    {
      Literal literal = Literal.ParseLiteral((string)Consume().value!);

      return literal switch
      {
        ByteLiteral b => b.Byte,
        CharLiteral c => Literal.ParseChar(c.Character),
        UShortLiteral us => us.UShort,
        ShortLiteral s => s.Short,
        UIntLiteral ui => ui.UInt,
        IntLiteral i => i.Int,
        ULongLiteral ul => ul.ULong,
        LongLiteral l => l.Long,
        FloatLiteral f => f.Float,
        DoubleLiteral d => d.Double,
        StringLiteral s => s.String,
        BooleanLiteral b => b.Boolean,
        _ => throw new UnreachableException()
      };
    }

    if (Peek(Token.Get(Token.Type.PAREN_BLOCK)))
      return Switch((Token[])Consume().value!, () => PreprocessExpr(output));

    if (Peek(Token.Get(Token.Type.IDENTIFIER)))
      return macros.ContainsKey((string)Consume().value!);

    if (Peek(Token.Get(Token.Type.DOLLAR)))
      return Switch(PreprocessOne(output), () => PreprocessExpr(output));

    Error("Invalid expression");
    throw new UnreachableException();
  }

  protected bool IsMacroEmpty(string name)
  {
    if (currentMacroArgs.TryGetValue(name, out var content) && content.Length > 0)
      return true;
    if (macros.TryGetValue(name, out var macro))
    {
      if (macro is SimpleMacro simple)
        return simple.Content.Length > 0;
      if (macro is ArgsMacro args)
        return args.Content.Length > 0;
      return false;
    }
    return false;
  }

  protected Token[] ExpandMacro(Macro macro)
  {
    if (macro is SimpleMacro simple)
      return Switch(simple.Content, Process);

    if (macro is ArgsMacro argsMacro)
    {
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
      Token[] ret = Switch(argsMacro.Content, Process);
      currentMacroArgs.Clear();
      return ret;
    }

    GenericMacro genericMacro = (macro as GenericMacro)!;
    Token[][] types = ParseArgs(Token.Type.ANGLE_BLOCK);
    if (types.Length != genericMacro.Generics.Length)
      Error($"Generic macro {genericMacro.Name} expects {genericMacro.Generics.Length} generic type arguments but {types.Length} were given");

    string genstrname = $"{genericMacro.Name}_{string.Join('_', types.Select(t => string.Join('_', t.Select(tk => tk.Stringify()))))}";
    Token[] genericName = [new(Token.Type.IDENTIFIER, tokenInfos.Peek(), genstrname)];

    if (GenericBlocks.Contains(genstrname))
      return genericName;

    foreach ((string name, Token[] body) in genericMacro.Generics.Zip(types))
      currentMacroArgs[name] = body;
    
    currentMacroArgs["self"] = genericName;

    Token[] decl = Switch(genericMacro.Content, Process);
    genericMacro.GenerationOutput.InsertRange(genericMacro.GenerationSpot, decl);
    GenericBlocks.Add(genstrname);
    currentMacroArgs.Clear();
    return genericName;
  }

  private Dictionary<string, Token[]> ProcessPlatformValues(TomlTable platform)
  {
    Dictionary<string, Token[]> pairs = [];
    Lexer.Lexer lexer = new([], "");
    foreach ((string key, object value) in platform)
    {
      string? val = value as string;
      if (val == null)
        Error("Invalid token string");
      pairs[key] = lexer.Process([.. val]);
    }
    return pairs;
  }

  private void InitMacros(TomlTable platform)
  {
    Dictionary<string, Token[]> pairs = ProcessPlatformValues(platform);
    foreach ((string name, Token[] content) in pairs)
      macros.Add(name, new SimpleMacro(name, content));
  }
}

public record Macro { }
public record SimpleMacro(string Name, Token[] Content) : Macro { }
public record ArgsMacro(string Name, Token[] Content, string[] Arguments, string? Variadic) : Macro { }
public record GenericMacro(string Name, string[] Generics, Token[] Content, int GenerationSpot, List<Token> GenerationOutput) : Macro { }
