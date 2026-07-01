using Lexer;

namespace Parser;

public enum NodeInstanceID
{
  
}

public class SyntaxInstance(NodeInstanceID id, Dictionary<string, (Type, object)> properties, Action<SyntaxInstance>? final)
{
  private readonly NodeInstanceID Id = id;
  private readonly Dictionary<string, (Type, object)> Properties = properties;
  public readonly Action<SyntaxInstance>? Final = final;
}

public partial class Syntax
{
  private readonly List<SyntaxNode> PreNodes = [];
  private Token wakeup = new(Token.Type.NULL);
  private readonly List<SyntaxNode> Nodes = [];

  public Syntax Wakeup(Token w)
  {
    wakeup = w;
    return this;
  }

  public Syntax Capture(Type type, string name, Func<object> get)
  {
    Nodes.Add(new CaptureDef(type, name, get));
    return this;
  }

  public Syntax Mandatory(Token token)
  {
    Nodes.Add(new Needed(token));
    return this;
  }

  public Syntax CapturePre(Type type, string name, Func<object> get)
  {
    PreNodes.Add(new CaptureDef(type, name, get));
    return this;
  }

  public Syntax MandatoryPre(Token token)
  {
    PreNodes.Add(new Needed(token));
    return this;
  }

  public SyntaxInstance? Build(Parser parser, NodeInstanceID id, Action<SyntaxInstance>? final)
  {
    Dictionary<string, (Type, object)> properties = [];

    parser.SavePeek();

    PreNodes.ForEach(node => node.Run(parser, properties) );

    if (!parser.Wakeup(wakeup))
    {
      parser.RestorePeek();
      return null;
    }

    Nodes.ForEach(node => node.Run(parser, properties) );

    return new SyntaxInstance(id, properties, final);
  }

  public SyntaxInstance? Build(Parser parser, NodeInstanceID id) => Build(parser, id, null);
}
