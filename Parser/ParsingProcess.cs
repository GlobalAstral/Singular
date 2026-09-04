using Lexer;

namespace Parser;

public record ParsingProcess(Token.Type Type, bool Consume, Func<TokenInfo, Statement> Factory) { }

public partial class Parser
{
  private void Init()
  {
    Wakeup(Token.Type.NAMESPACE, true, info =>
    {
      if (!InGlobalScope())
        Error("Namespaces cannot be outside of global scope");
      string name = (string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
      namespaces.Push(name);
      Token[] content = (Token[])TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
      Statement[] statements = Switch(content, Process);
      namespaces.Pop();
      return new Group(statements);
    });

    Wakeup(Token.Type.CURLY_BLOCK, false, info =>
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
    });

    Wakeup(Token.Type.FUN, true, ParseFunction);

    Wakeup(Token.Type.RETURN, true, info =>
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
      Semi();
      return scopeContext.ResolveDefers(new Return(expression));
    });

    Wakeup(Token.Type.STRUCT, true, info => ParseComposite(info, Composite.Type.STRUCT, s => new StructDecl(s)));

    Wakeup(Token.Type.UNION, true, info => ParseComposite(info, Composite.Type.UNION, s => new UnionDecl(s)));

    Wakeup(Token.Type.VAR, true, info =>
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
      Semi();
      Variable variable = new(modifiers, type ?? val!.GetReturnType(), name);
      AddVariable(variable);
      return new VariableDecl(variable, val);
    });

    Wakeup(Token.Type.TYPE, true, info =>
    {
      if (!InGlobalScope())
        Error("Type declarations cannot be outside of global scope");
      string name = MangleIdentifier();
      TryConsumeError(Token.Get(Token.Type.EQUALS));
      DataType type = ParseType();
      Semi();
      aliases[name] = type;
      return new TypeDefinition(name, type);
    });

    Wakeup(Token.Type.DEFER, true, info =>
    {
      if (!InScope(out var scope))
        Error("Cannot defer outside of a scope");
      scope!.Defers.Push(ProcessOne());
      return new Nop();
    });

    Wakeup(Token.Type.IF, true, info =>
    {
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      Expression cond = Switch(condition, () => ParseExpression(BooleanType.INSTANCE));
      Statement body = ProcessOne();
      Statement? other = TryConsume(Token.Get(Token.Type.ELSE)) ? ProcessOne() : null;
      return new IfStatement(cond, body, other);
    });

    Wakeup(Token.Type.WHILE, true, info =>
    {
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      Expression cond = Switch(condition, () => ParseExpression(BooleanType.INSTANCE));
      
      currentContext.Push(new LoopContext());
      Statement body = ProcessOne();
      currentContext.Pop();

      return new WhileStmt(cond, body);
    });

    Wakeup(Token.Type.DO, true, info =>
    {
      currentContext.Push(new LoopContext());
      Statement body = ProcessOne();
      currentContext.Pop();

      TryConsumeError(Token.Get(Token.Type.WHILE));
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      Expression cond = Switch(condition, () => ParseExpression(BooleanType.INSTANCE));
      return new DoWhileStmt(cond, body);
    });

    Wakeup(Token.Type.LOOP, true, info =>
    {
      currentContext.Push(new LoopContext());
      Statement body = ProcessOne();
      currentContext.Pop();

      return new WhileStmt(new LiteralExpr(new BooleanLiteral(true)), body);
    });

    Wakeup(Token.Type.SEMI, true, info => new Nop());

    Wakeup(Token.Type.FOR, true, info =>
    {
      Token[] condition = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      
      int saved = InScope(out var scope) ? scope!.Locals.Count : globals.Count;

      currentContext.Push(new LoopContext());

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

      currentContext.Pop();

      if (InScope(out var s))
        s!.Locals.RemoveAll(v => v.Name == var.Name);
      else
        globals.RemoveAll(v => v.Name == var.Name);

      return new ForStmt(Init, cond, update, body);
    });

    Wakeup(Token.Type.BREAK, true, info =>
    {
      Context? current = currentContext.Peek();
      if (current is not LoopContext && current is not SwitchContext)
        Error("Cannot break outside of loop or switch statement");
      Semi();
      return new BreakStmt();
    });

    Wakeup(Token.Type.CONTINUE, true, info =>
    {
      if (currentContext.Peek() is not LoopContext)
        Error("Cannot continue outside of loop");
      Semi();
      return new ContinueStmt();
    });

    Wakeup(Token.Type.SWITCH, true, info =>
    {
      Expression expression = Switch((Token[])TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!, () => ParseExpression(null));
      DataType caseType = expression.GetReturnType();
      currentContext.Push(new SwitchContext());

      ((Expression, Statement)[], Statement?) Cases = Switch((Token[])TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!, () =>
      {
        List<(Expression, Statement)> cases = [];
        Statement? Default = null;
        while (HasPeek())
        {
          if (TryConsume(Token.Get(Token.Type.CASE)))
          {
            Expression c = ParseExpression(caseType);
            TryConsumeError(Token.Get(Token.Type.COLON));
            Statement statement = ProcessOne();
            cases.Add((c, statement));
          }
          else if (TryConsume(Token.Get(Token.Type.DEFAULT)))
          {
            TryConsumeError(Token.Get(Token.Type.COLON));
            Statement statement = ProcessOne();
            Default = statement;
          }
          else
            Error("Invalid Switch case or default");
        }
        return (cases.ToArray(), Default);
      });

      currentContext.Pop();
      return new SwitchStmt(expression, Cases.Item1, Cases.Item2);
    });

    Wakeup(Token.Type.EXTERN, true, info =>
    {
      if (!InGlobalScope())
        Error("Extern cannot be outside of global scope");
      if (Peek(Token.Get(Token.Type.LITERAL)))
      {
        Literal lit = Literal.ParseLiteral((string) Consume().value!);
        if (lit is not StringLiteral)
          Error($"Extern ABI can only be a string literal");
        string s = ((StringLiteral) lit).String;
        return ParseABI(info, s);
      }
      return ParseExtern(info, MangleIdentifier);
    });

    Wakeup(Token.Type.ENUM, true, info =>
    {
      if (!InGlobalScope())
        Error("Enum cannot be outside of global scope");
      
      string name = MangleIdentifier();

      Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;

      long count = 0;
      Dictionary<string, long> entries = [];

      Switch(body, () =>
      {
        string entry = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
        if (TryConsume(Token.Get(Token.Type.EQUALS)))
        {
          Expression expression = ParseExpression(null);
          long val = ParseEnumConstant(expression, entries);
          count = val;
        }
        entries[entry] = count++;
      }, Token.Get(Token.Type.COMMA));

      long max = entries.Values.Max();
      long min = entries.Values.Min();

      (DataType dataType, Func<long, Literal> factory) = (min, max) switch
      {
        (>= byte.MinValue, <= byte.MaxValue)     => (ByteType.INSTANCE, ByteType.Factory),
        (>= sbyte.MinValue, <= sbyte.MaxValue)   => (CharType.INSTANCE, CharType.Factory),
        (>= ushort.MinValue, <= ushort.MaxValue) => (UShortType.INSTANCE, UShortType.Factory),
        (>= short.MinValue, <= short.MaxValue)   => (ShortType.INSTANCE, ShortType.Factory),
        (>= uint.MinValue, <= uint.MaxValue)     => (UIntType.INSTANCE, UIntType.Factory),
        (>= int.MinValue, <= int.MaxValue)       => (IntType.INSTANCE, IntType.Factory),
        (>= 0, _)                                => (ULongType.INSTANCE, ULongType.Factory),
        _                                        => (LongType.INSTANCE, LongType.Factory)
      };

      if (aliases.ContainsKey(name))
        Error($"Type {name} already exists");
      
      List<Statement> statements = [];

      statements.Add(new TypeDefinition(name, dataType));
      aliases[name] = dataType;

      DataType varType = References.GetAliasType(name, dataType);

      namespaces.Push(name);
      foreach (var pair in entries)
      {
        Variable var = new(new ModifierHandler().Static(), varType, MangleIdentifier(pair.Key));
        AddVariable(var);
        Expression expr = new LiteralExpr(factory(pair.Value));
        statements.Add(new VariableDecl(var, expr));
      }
      namespaces.Pop();
      return new Group([ .. statements ]);
    });
  }
}
