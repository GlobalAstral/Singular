using Lexer;
namespace Parser;

public partial class Parser
{
  private readonly List<Syntax> syntaxes = [];

  private readonly Stack<ClassContext> currentClass = [];
  private readonly List<ClassContext> classes = [];

  private string[] ParseGenerics()
  {
    if (Peek(ANGLE_BLOCK))
    {
      List<Token> generics_body = (List<Token>)Consume().value!;
      List<string> generics = [];
      Switch(generics_body, () => Alternate(COMMA, () => generics.Add((string) TryConsumeError(IDENTIFIER).value!) ));
      return [.. generics];
    }
    return [];
  }

  private void RegisterSyntaxes()
  {
    syntaxes.Add(
      new Syntax(NodeInstanceID.CLASS_DECL)
        .CapturePre<ModifierHandler>("modifiers", () => GetModifiers(handler =>
        {
          if (handler.IsMutable)
            Error("Class cannot be mutable");
        }))
        .Wakeup(new(Token.Type.CLASS))
        .Capture<string>("name", () => (string)TryConsumeError(IDENTIFIER).value!)
        .Capture<string[]>("generics", ParseGenerics)
        .Capture<List<Token>>("body", () => (List<Token>)TryConsumeError(CURLY_BLOCK).value!)
        .DoNotInstantiate()
        .Finalize(instance =>
        {
          ModifierHandler modifiers = instance["modifiers"];
          string name = instance["name"];
          string[] generics = instance["generics"];
          List<Token> body = instance["body"];

          ClassContext context = new(modifiers, name, generics);

          currentClass.Push(context);
          Switch(body, () => Parse());
          classes.Add(currentClass.Pop());
        })
    );
  }
}

