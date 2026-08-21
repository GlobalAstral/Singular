using Lexer;
namespace Parser;

public partial class Parser
{
  private readonly List<Syntax> syntaxes = [];

  private readonly Stack<string> namespaces = [];

  private void RegisterSyntaxes()
  {
    Register(
      new Syntax(NodeInstanceID.NAMESP)
        .Wakeup(this, new(Token.Type.NAMESPACE))
        .Capture<string>("name", () => TryConsumeError(new(Token.Type.IDENTIFIER)).value!)
        .Capture<Token[]>("body", () => TryConsumeError(new(Token.Type.CURLY_BLOCK)).value!)
        .DoNotInstantiate()
        .Finalize(instance =>
        {
          string name = instance["name"];
          Token[] body = instance["body"];
          namespaces.Push(name);
          Switch(body, () => Parse());
          namespaces.Pop();
        })
    );

    
  }

  private void Register(Syntax syntax) => syntaxes.Add(syntax);
}

