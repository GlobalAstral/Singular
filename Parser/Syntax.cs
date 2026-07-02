using System.ComponentModel;
using Lexer;

namespace Parser;

public enum NodeInstanceID
{
  CLASS_DECL,
  NULL
}

public class SyntaxInstance(NodeInstanceID id, Dictionary<string, (Type, object)> properties)
{
  public SyntaxInstance() : this(NodeInstanceID.NULL, []) { }
  public readonly NodeInstanceID Id = id;
  private readonly Dictionary<string, (Type, object)> Properties = properties;

  private dynamic GetValue(string key)
  {
    var (type, value) = Properties[key];
    return Convert.ChangeType(value, type);
  }

  public dynamic this[string key] {
    get => GetValue(key);
  }
}

public partial class Syntax(NodeInstanceID id)
{
  private readonly List<SyntaxNode> PreNodes = [];
  private Token wakeup = new(Token.Type.NULL);
  private readonly List<SyntaxNode> Nodes = [];
  private Action<SyntaxInstance>? finalize = null;
  private bool DoNotInstantiate_ = false;
  public Syntax Wakeup(Token w)
  {
    wakeup = w;
    return this;
  }

  public Syntax Capture<T>(string name, Func<object> get)
  {
    Nodes.Add(new CaptureDef(typeof(T), name, get));
    return this;
  }

  public Syntax Mandatory(Token token)
  {
    Nodes.Add(new Needed(token));
    return this;
  }

  public Syntax CapturePre<T>(string name, Func<object> get)
  {
    PreNodes.Add(new CaptureDef(typeof(T), name, get));
    return this;
  }

  public Syntax MandatoryPre(Token token)
  {
    PreNodes.Add(new Needed(token));
    return this;
  }

  public Syntax Finalize(Action<SyntaxInstance> action)
  {
    finalize = action;
    return this;
  }

  public Syntax DoNotInstantiate()
  {
    DoNotInstantiate_ = true;
    return this;
  }

  public SyntaxInstance? Build(Parser parser)
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

    SyntaxInstance instance = new(id, properties);

    finalize?.Invoke(instance);

    return DoNotInstantiate_ ? null : instance;
  }
}
