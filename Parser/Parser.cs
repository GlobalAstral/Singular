using Lexer;

namespace Parser;

public partial class Parser(Token[] tokens) : Processor<Token, Statement>(tokens, s => s is Nop)
{
  private readonly Stack<string> namespaces = [];
  private readonly List<Function> functions = [];
  private readonly Stack<Context> currentContext = [];
  private readonly Stack<DataType?> typeCheckerContext = [];
  private readonly List<Variable> globals = [];
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
      
      Context? current = currentContext.Peek();

      FunctionContext? fctx = current is FunctionContext ctx ? ctx : null;
      ScopeContext? sctx = current is ScopeContext c ? c : null;
      ScopeContext context = new(fctx, sctx);
      currentContext.Push(context);

      List<Statement> statements = [.. Switch(content, Process)];

      Statement toadd = context.ResolveDefers();

      if (!(toadd is Group group && group.Content.Length == 0))
        statements.Add(toadd);

      currentContext.Pop();

      return new Scope([.. statements]);
    },
      
    () => Wakeup(Token.Type.FUN, true, ParseFunction,
    
    () => Wakeup(Token.Type.RETURN, true, () =>
    {
      Context? current = currentContext.Peek();
      if (!InFunction())
        Error("Cannot return outside of Function Context");

      ScopeContext scopeContext = (ScopeContext)current;
      FunctionContext context = scopeContext.FunctionContext!;

      if (TryConsume(Token.Get(Token.Type.SEMI)))
      {
        if (context.ReturnType != null)
          Error($"Cannot return nothing in a function returning {context.ReturnType}");
        return scopeContext.ResolveDefers(new Return(null));
      }
      
      Expression expression = ParseExpression(context.ReturnType);

      DataType? temp = expression.GetReturnType();
      if (context.ReturnType != temp)
      {
        string t = context.ReturnType == null ? "nothing" : $"{context.ReturnType}";
        Error($"Cannot return {temp} in a function returning {t}");
      }
      TryConsumeError(Token.Get(Token.Type.SEMI));
      return scopeContext.ResolveDefers(new Return(expression));
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
      Variable variable = new(modifiers, type, name);
      AddVariable(variable);
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
    
    () => Wakeup(Token.Type.DEFER, true, () =>
    {
      if (!InScope(out var scope))
        Error("Cannot defer outside of a scope");
      scope!.Defers.Push(ProcessOne());
      return new Nop();
    },
    
    () => Wakeup(Token.Type.IF, true, () =>
    {
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      Expression cond = Switch(condition, () => ParseExpression(BooleanType.INSTANCE));
      Statement body = ProcessOne();
      Statement? other = TryConsume(Token.Get(Token.Type.ELSE)) ? ProcessOne() : null;
      return new IfStatement(cond, body, other);
    },
    
    () => null
    
    ))))))))));
    
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
