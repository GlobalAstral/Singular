using Lexer;

namespace Parser;

public partial class Syntax
{
  private interface SyntaxNode
  {
    public void Run(Parser parser, Dictionary<string, (Type, object)> properties);
  }

  private readonly struct CaptureDef(Type type, string name, Func<object> factory) : SyntaxNode
  {
    readonly Type Type = type;
    readonly string Name = name;
    readonly Func<object> Factory = factory;

    public void Run(Parser parser, Dictionary<string, (Type, object)> properties)
    {
      properties[Name] = (Type, Factory());
    }
  }

  private readonly struct Needed(Token token) : SyntaxNode
  {
    readonly Token Token = token;
    public void Run(Parser parser, Dictionary<string, (Type, object)> properties)
    {
      parser.Require(Token);
    }
  }
}
