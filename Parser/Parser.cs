using Lexer;

namespace Parser;

public partial class Parser(Token[] tokens) : Processor<Token, Statement>(tokens)
{
  private readonly Stack<string> namespaces = [];
  private readonly List<Function> functions = [];
  private readonly Stack<Context> currentContext = [];
  private readonly Stack<DataType?> typeCheckerContext = [];
  private readonly List<Variable> locals = [];

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
      
      int saved = locals.Count;
      Statement[] statements = Switch(content, Process);
      locals.RemoveRange(saved, locals.Count - saved);

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
      
      currentContext.Push(new FunctionContext(retType, args));
      
      Statement? body = TryConsume(Token.Get(Token.Type.SEMI)) ? null : ProcessOne();
      
      currentContext.Pop();
      
      Function f = new(modifiers, name, args, retType, body);
      Function? found = functions.Find(ele => ele.Equals(f));
      
      if (found == null)
      {
        functions.Add(f);
        return new FunctionDecl(f);
      }
      if (found.Body == null)
      {
        found.Body = f.Body;
        return new FunctionDecl(found);
      }
      Error($"Function {name} already exists");
      return null;
    },
    
    () => Wakeup(Token.Type.RETURN, true, () =>
    {
      if (currentContext.Count == 0 || currentContext.Peek() is not FunctionContext)
        Error("Cannot return outside of Function Context");

      FunctionContext context = (FunctionContext) currentContext.Peek();

      if (TryConsume(Token.Get(Token.Type.SEMI)))
      {
        if (context.ReturnType != null)
          Error($"Cannot return nothing in a function returning {context.ReturnType}");
        return new Return(null);
      }
      
      Expression expression = ParseExpression(context.ReturnType);

      DataType? temp = expression.GetReturnType();
      if (context.ReturnType != temp)
      {
        string t = context.ReturnType == null ? "nothing" : $"{context.ReturnType}";
        Error($"Cannot return {temp} in a function returning {t}");
      }

      TryConsumeError(Token.Get(Token.Type.SEMI));
      return new Return(expression);
    }, () => null))

    )

    );
    
    if (ret == null)
      Error("Syntax Error");
    return ret;
  }
}
