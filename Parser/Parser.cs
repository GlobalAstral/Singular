using System.Runtime.InteropServices;
using Lexer;

namespace Parser;

public partial class Parser(Token[] tokens) : Processor<Token, Statement>(tokens)
{
  private readonly Stack<string> namespaces = [];
  private readonly List<Function> functions = [];
  private readonly Stack<Context> currentContext = [];
  private readonly Stack<DataType?> typeCheckerContext = [];
  private readonly List<Variable> locals = [];
  private readonly Dictionary<string, Composite> composites = [];
  private readonly Dictionary<string, DataType> aliases = [];
  private uint IgnoringExpression = 0;
  private bool extendedExpr = true;
  private readonly Stack<int> LocalsSnapshots = [];

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
      
      SaveSnapshot();
      Statement[] statements = Switch(content, Process);
      RestoreSnapshot();
      return new Scope(statements);
    },
      
    () => Wakeup(Token.Type.FUN, true, ParseFunction,
    
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
      return ResolveDefers(new Return(expression));
    },

    () => Wakeup(Token.Type.STRUCT, true, () => ParseComposite(Composite.Type.STRUCT, s => new StructDecl(s)),
    
    () => Wakeup(Token.Type.UNION, true, () => ParseComposite(Composite.Type.UNION, s => new UnionDecl(s)),
    
    () => Wakeup(Token.Type.VAR, true, () =>
    {
      ModifierHandler modifiers = GetModifiers(handler => {});
      DataType type = ParseType();
      string name = MangleIdentifier();
      Expression? val = null;
      if (TryConsume(Token.Get(Token.Type.EQUALS)))
        val = ParseExpression(type);
      TryConsumeError(Token.Get(Token.Type.SEMI));
      if (locals.Any(v => v.Name == name))
        Error($"Variable {name} already exists");
      Variable variable = new(modifiers, type, name);
      locals.Add(variable);
      return new VariableDecl(variable, val);
    },
    
    () => Wakeup(Token.Type.TYPE, true, () =>
    {
      string name = MangleIdentifier();
      TryConsumeError(Token.Get(Token.Type.EQUALS));
      DataType type = ParseType();
      TryConsumeError(Token.Get(Token.Type.SEMI));
      aliases[name] = type;
      return new TypeDefinition(name, type);
    },
    
    () => null
    
    ))))))));
    
    if (ret == null)
    {
      IgnoringExpression++;
      Expression expression = ParseExpression(null);
      IgnoringExpression--;
      TryConsumeError(Token.Get(Token.Type.SEMI));
      ret = new IgnoredExpr(expression);
    }
    return ret;
  }
}
