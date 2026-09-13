using System.Diagnostics;
using Lexer;

namespace Parser;

public partial class Parser
{
  private bool PeekDblCln() => Peek(Token.Get(Token.Type.COLON)) && Peek(Token.Get(Token.Type.COLON), 1);
  private bool TryConsumeDblCln()
  {
    if (PeekDblCln())
    {
      Consume(2);
      return true;
    }
    return false;
  }

  protected enum SymbolType
  {
    GlobalDeclaration,
    LocalVariableDecl,
    NamedType,
    Variable,
    CompositeInternal,
  }

  protected string Mangle(SymbolType type)
  {
    if (TryConsume(Token.Get(Token.Type.AT)))
      return NoMangle();

    return type switch
    {
      SymbolType.GlobalDeclaration => RegularMangling(),
      SymbolType.LocalVariableDecl => RegularMangling(),
      SymbolType.NamedType => RegularMangling(),
      SymbolType.Variable => RegularMangling(),
      SymbolType.CompositeInternal => CompositeMangling(),
      _ => throw new UnreachableException(),
    };
  }

  protected static string QualifyIdentifier(string ident) => $"{ident.Length}{ident}";
  protected static string QualifyIdentifier(string prefix, string ident) => $"{prefix}_{QualifyIdentifier(ident)}";
  protected static string QualifyNameParts(List<string> name_parts) => string.Concat(name_parts.Select(name => QualifyIdentifier(name)));
  protected static string QualifyNameParts(string prefix, List<string> name_parts) => $"{prefix}_{QualifyNameParts(name_parts)}";

  protected string RegularMangling()
  {
    List<string> name_parts = [];
    
    if (PeekDblCln())
    {
      name_parts.AddRange(namespaces.Reverse());
      while (TryConsumeDblCln())
      {
        string part = NoMangle();
        name_parts.Add(part);
      }
      return QualifyNameParts("M", name_parts);
    }

    string name = NoMangle();
    
    if (PeekDblCln())
    {
      name_parts.Add(name);
      while (TryConsumeDblCln())
      {
        string part = NoMangle();
        name_parts.Add(part);
      }
      return QualifyNameParts("M", name_parts);
    }

    name_parts.AddRange(namespaces.Reverse());
    name_parts.Add(name);
    return QualifyNameParts("M", name_parts);
  }

  protected string NoMangle() => (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;

  protected string CompositeMangling()
  {
    if (currentContext.Count == 0 || currentContext.Peek() is not CompositeContext)
      Error("Cannot mangle composite interior");
    CompositeContext context = (currentContext.Peek() as CompositeContext)!;
    string name = NoMangle();
    return $"{context.Comp.Name}{name.Length}{name}";
  }
}
