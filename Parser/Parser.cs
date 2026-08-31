using Lexer;

namespace Parser;

public partial class Parser(Token[] tokens) : Processor<Token, Statement>(tokens, s => s is Nop)
{
  private readonly Stack<string> namespaces = [];
  private readonly List<Function> functions = [];
  private readonly Stack<Context> currentContext = [];
  private readonly Stack<ScopeContext> activeScopes = [];
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
      ScopeContext context = new(fctx);
      currentContext.Push(context);
      activeScopes.Push(context);

      List<Statement> statements = [.. Switch(content, Process)];

      Statement toadd = context.ResolveDefers();

      if (!(toadd is Group group && group.Content.Length == 0))
        statements.Add(toadd);

      activeScopes.Pop();
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
      DataType? type = null;
      
      if (!TryConsume(Token.Get(Token.Type.INFER)))
        type = ParseType();

      string name = MangleIdentifier();
      Expression? val = null;
      if (TryConsume(Token.Get(Token.Type.EQUALS)))
        val = ParseExpression(type);
      if (type == null && val == null)
        Error($"Cannot infer type of uninitialized variable {name}");
      TryConsumeError(Token.Get(Token.Type.SEMI));
      Variable variable = new(modifiers, type ?? val!.GetReturnType(), name);
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
    
    () => Wakeup(Token.Type.WHILE, true, () =>
    {
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      Expression cond = Switch(condition, () => ParseExpression(BooleanType.INSTANCE));
      
      currentContext.Push(new LoopContext());
      Statement body = ProcessOne();
      currentContext.Pop();

      return new WhileStmt(cond, body);
    },
    
    () => Wakeup(Token.Type.DO, true, () =>
    {
      currentContext.Push(new LoopContext());
      Statement body = ProcessOne();
      currentContext.Pop();

      TryConsumeError(Token.Get(Token.Type.WHILE));
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      Expression cond = Switch(condition, () => ParseExpression(BooleanType.INSTANCE));
      return new DoWhileStmt(cond, body);
    },
    
    () => Wakeup(Token.Type.LOOP, true, () =>
    {
      currentContext.Push(new LoopContext());
      Statement body = ProcessOne();
      currentContext.Pop();

      return new WhileStmt(new LiteralExpr(new BooleanLiteral(true)), body);
    },
    
    () => Wakeup(Token.Type.SEMI, true, () => new Nop(),
    
    () => Wakeup(Token.Type.FOR, true, () =>
    {
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      
      int saved = InScope(out var scope) ? scope!.Locals.Count : globals.Count;

      (Statement Init, Expression cond, Statement update, Variable var) = Switch(condition, () =>
      {
        Variable variable = new(new ModifierHandler().Mutable(), ParseType(), ParseIdentifier());
        TryConsumeError(Token.Get(Token.Type.IN));
        bool reverse = TryConsume(Token.Get(Token.Type.EXCLAMATION));
        Expression start = ParseExpression(variable.Type);

        Statement init = new VariableDecl(variable, start);
        AddVariable(variable);

        TryConsumeError(Token.Get(Token.Type.COMMA));
        bool inclusive = TryConsume(Token.Get(Token.Type.EQUALS));
        Expression end = ParseExpression(variable.Type);

        BinaryExpr.BinaryOp op = reverse ? (inclusive ? BinaryExpr.BinaryOp.GreaterEqual : BinaryExpr.BinaryOp.Greater) : (inclusive ? BinaryExpr.BinaryOp.LessEqual : BinaryExpr.BinaryOp.Less);

        Expression condition = new BinaryExpr(new IdentifierExpression(variable), end, op);

        Expression inc = TryConsume(Token.Get(Token.Type.COMMA)) ? ParseExpression(variable.Type) : new PostIncrement(new IdentifierExpression(variable), 1);

        Statement update = new IgnoredExpr(inc);

        return (init, condition, update, variable);
      });
      
      Statement body = ProcessOne();

      if (InScope(out var s))
        s!.Locals.RemoveAll(v => v.Name == var.Name);
      else
        globals.RemoveAll(v => v.Name == var.Name);

      return new ForStmt(Init, cond, update, body);
    },
    
    () => null

    //TODO Break (Switch and Loops)
    //TODO Continue (Loops)
    //TODO Switch
    //TODO Switch Expression (IDK)
    //TODO Raw C code (Easy)
    //TODO extern (Not too hard, tedious)
    
    )))))))))))))));
    
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
