using Lexer;

namespace Parser;

public partial class Parser(Token[] tokens) : Processor<Token, Statement>(tokens)
{
  private readonly Stack<string> namespaces = [];
  private readonly List<Function> functions = [];

  public override Statement ProcessOne()
  {
    Statement? ret =

    Wakeup(Token.Type.NAMESPACE, true, () =>
    {
      string name = (string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
      namespaces.Push(name);
      Token[] content = (Token[])TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
      Statement[] statements = Switch(content, Process);
      namespaces.Pop();
      return new Group(statements);
    },

    () => Wakeup(Token.Type.CURLY_BLOCK, false, () =>
    {
      Token[] content = (Token[])Consume().value!;
      Statement[] statements = Switch(content, Process);
      return new Scope(statements);
    },
      
    () => Wakeup(Token.Type.FUN, true, () =>
    {
      ModifierHandler modifiers = GetModifiers(handler =>
      {
        if (handler.IsMutable) Error("Function cannot be mutable");
        if (handler.IsReadonly) Error("Function cannot be readonly");
      });

      string name = MangleIdentifier();
      Variable[] args = ParseArgs();
      DataType? retType = TryConsume(Token.Get(Token.Type.COLON)) ? ParseType() : null;
      Statement? body = TryConsume(Token.Get(Token.Type.SEMI)) ? null : ProcessOne();
      Function f = new(modifiers, name, args, retType, body);
      Function? found = functions.Find(ele => ele.Equals(f));
      if (found == null)
      {
        functions.Add(f);
        return new FunctionDecl(f);
      }
      if (found.Body == null)
        functions.Remove(found);
      functions.Add(f);
      return new FunctionDecl(f);

    }, () => null)

    )

    );
    

    if (ret == null)
      Error("Syntax Error");
    return ret;
  }
}
