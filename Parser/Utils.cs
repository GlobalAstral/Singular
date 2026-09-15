using System.Diagnostics;
using Lexer;

namespace Parser;

public class ModifierHandler
{
  public bool IsStatic = false;
  public bool IsMutable = false;
  public ModifierHandler Static()
  {
    IsStatic = true;
    return this;
  }

  public ModifierHandler Mutable()
  {
    IsMutable = true;
    return this;
  }

  public override bool Equals(object? obj)
  {
    if (obj is not ModifierHandler other)
      return false;
    
    if (IsStatic != other.IsStatic)
      return false;
    
    if (IsMutable != other.IsMutable)
      return false;
    
    return true;
  }

  public override int GetHashCode() => HashCode.Combine(IsStatic, IsMutable);

  public static bool operator ==(ModifierHandler a, ModifierHandler b) => a.Equals(b);
  public static bool operator !=(ModifierHandler a, ModifierHandler b) => !a.Equals(b);

  public override string ToString()
  {
    string s = IsStatic ? "static" : ""; 
    string m = IsMutable ? "mutable" : ""; 
    string temp = $"{s} {m}".Trim();
    return $"[{temp}]";
  }
}

public partial class Parser
{
  protected T WithModifiers<T>(Func<ModifierHandler, T> action)
  {
    ModifierHandler handler = new();
    
    if (TryConsume(Token.Get(Token.Type.STATIC)))
      handler.Static();
    
    if (TryConsume(Token.Get(Token.Type.MUT)))
      handler.Mutable();
    
    return action(handler);
  }

  protected void WithModifiers(Action<ModifierHandler> action) => WithModifiers(handler =>
  {
    action(handler);
    return 0;
  });

  protected ModifierHandler GetModifiers(Action<ModifierHandler> sanitize)
  {
    return WithModifiers(handler =>
    {
      sanitize(handler);
      return handler;
    });
  }

  protected bool PeekIdentifier() => PeekDblCln() || Peek(Token.Get(Token.Type.AT)) && Peek(Token.Get(Token.Type.IDENTIFIER), 1) || Peek(Token.Get(Token.Type.IDENTIFIER), 0);
  
  protected DataType ParseType()
  {
    DataType? dataType = null;
    if (TryConsume(Token.Get(Token.Type.STAR)))
    {
      bool mutable = TryConsume(Token.Get(Token.Type.MUT));
      dataType = References.GetPointerType(ParseType(), mutable);
    }
    else if (TryConsume(Token.Get(Token.Type.EXCLAMATION)))
      dataType = References.GetErrorUnionType(ParseType());
    else if (TryConsume(Token.Get(Token.Type.BYTE)))
      dataType = ByteType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.CHAR)))
      dataType = CharType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.USHORT)))
      dataType = UShortType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.SHORT)))
      dataType = ShortType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.UINT)))
      dataType = UIntType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.INT)))
      dataType = IntType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.ULONG)))
      dataType = ULongType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.LONG)))
      dataType = LongType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.BOOLEAN)))
      dataType = BooleanType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.FLOAT)))
      dataType = FloatType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.DOUBLE)))
      dataType = DoubleType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.DYNAMIC)))
      dataType = DynamicType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.STRING)))
      dataType = StringType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.ERROR)))
      dataType = ErrorType.INSTANCE;
    else if (TryConsume(Token.Get(Token.Type.SELF)))
    {
      if (currentContext.Peek() is CompositeContext context)
        return References.GetCompositeType(context.Comp.Name, context.Comp, false);
      Error("Cannot use Self outside of Composite context");
    }
    else if (TryConsume(Token.Get(Token.Type.FUN)))
    {
      (Variable[] arguments, bool variadic) = ParseArgs();
      DataType[] args = [.. arguments.Select(v => v .Type)];
      DataType? result = null;
      if (TryConsume(Token.Get(Token.Type.COLON)))
        result = ParseType();
      return References.GetFunctionType(result, args, variadic);
    }
    else if (PeekIdentifier())
    {
      string ident = Mangle(SymbolType.NamedType);
      if (aliases.TryGetValue(ident, out var value))
        dataType = References.GetAliasType(ident, value);
      else if (composites.TryGetValue(ident, out var val))
        dataType = References.GetCompositeType(ident, val, false);
    }
    else if (Peek(Token.Get(Token.Type.TUPLE)) || Peek(Token.Get(Token.Type.VARIANT)))
    {
      Composite.Type compType = TryConsume(Token.Get(Token.Type.TUPLE)) ? Composite.Type.STRUCT : Peek() == Consume() ? Composite.Type.UNION : throw new UnreachableException();
      Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
      int count = 1;
      List<Variable> fields = [];
      Switch(body, () =>
      {
        DataType type = ParseType();
        string name = PeekIdentifier() ? NoMangle() : $"item{count++}";
        fields.Add(new(new ModifierHandler().Mutable(), type, name));
      }, Token.Get(Token.Type.COMMA));
      string compName = $"S_{GetHashCode()}_{string.Join('_', fields.Select(v => $"{v.Type.Stringify()}_{v.Name}"))}";
      dataType = References.GetCompositeType(compName, new(compName, [ .. fields ], [], compType), true);
    }
    
    if (dataType == null)
      Error("Expected Type");
    
    if (Peek(Token.Get(Token.Type.SQUARE_BLOCK)))
    {
      Token[] temp = (Token[])Consume().value!;
      Expression? size = temp.Length == 0 ? null : Switch(temp, () => ParseExpression(ULongType.INSTANCE));
      dataType = new ArrayType(dataType, size);
    }
    return dataType;
  }

  protected (Variable[] args, bool variadic) ParseArgs()
  {
    Token[] args = (Token[])TryConsumeError(Token.Get(Token.Type.PAREN_BLOCK)).value!;
    List<Variable> arguments = [];
    bool variadic = false;
    Switch(args, () => WithModifiers(handler =>
    {
      if (Peek(Token.Get(Token.Type.DOT)) && Peek(Token.Get(Token.Type.DOT), 1) && Peek(Token.Get(Token.Type.DOT), 2))
      {
        Consume(3);
        variadic = true;
        return;
      }
      if (handler.IsStatic)
        Error("Argument cannot be static");

      DataType t = ParseType();
      string ident = Mangle(SymbolType.LocalVariableDecl);
      if (arguments.Any(v => v.Name == ident))
        Error($"Function type cannot have duplicate arguments");
      arguments.Add(new Variable(handler, t, ident));
    }), Token.Get(Token.Type.COMMA));
    return ([.. arguments], variadic);
  }

  protected bool InFunction(out FunctionContext? context)
  {
    if (currentContext.Count > 0 && currentContext.Peek() is ScopeContext ctx && ctx.FunctionContext != null)
    {
      context = ctx.FunctionContext;
      return true;
    }
    context = null;
    return false;
  }

  protected bool InFunction() => InFunction(out var _);

  protected bool InScope(out ScopeContext? context)
  {
    if (currentContext.Count > 0 && currentContext.Peek() is ScopeContext ctx)
    {
      context = ctx;
      return true;
    }
    context = null;
    return false;
  }

  protected bool InScope() => InScope(out var _);

  protected bool InGlobalScope() => currentContext.Count == 0 || currentContext.Peek() == null;

  protected Variable? SearchVariable(string name)
  {
    Variable? found;
    foreach (ScopeContext scope in activeScopes)
    {
      found = scope!.Locals.Find(v => v.Name == name);
      if (found != null)
        return found;

      if (scope.FunctionContext != null)
      {
        found = scope.FunctionContext.Arguments.ToList().Find(v => v.Name == name);
        if (found != null)
          return found;
      }
    }

    found = globals.Find(v => v.Name == name);
    if (found != null)
      return found;

    return null;
  }

  protected void AddVariable(Variable variable)
  {
    if (SearchVariable(variable.Name) != null)
      Error($"Variable {variable.Name} already exists");
    
    if (InScope(out var scope))
    {
      scope!.Locals.Add(variable);
      return;
    }

    globals.Add(variable);
  }

  private Statement ParseFunction(TokenInfo info, Func<string> namingConvention) {
    if (!InGlobalScope() && currentContext.Peek() is not CompositeContext)
      Error("Functions cannot be outside of global scope");
    ModifierHandler modifiers = GetModifiers(handler => { if (handler.IsMutable) Error("Function cannot be mutable"); handler.Mutable(); });

    string name = namingConvention();
    
    (Variable[] args, bool variadic) = ParseArgs();
    DataType? retType = TryConsume(Token.Get(Token.Type.COLON)) ? ParseType() : null;
    
    currentContext.Push(new FunctionContext(retType, args));

    Function f = new(modifiers, name, args, retType, null, variadic);
    Function? found = functions.Find(ele => ele.Equals(f));

    if (found != null && found.Body != null)
      Error($"Function {name} already exists");

    if (found == null)
      functions.Add(f);
    
    Statement? body = TryConsume(Token.Get(Token.Type.SEMI)) ? null : ProcessOne();
    currentContext.Pop();

    if (found != null && found.Body == null)
    {
      if (body == null)
        Error("Cannot declare a function more than once");
      found.Body = body;
      return new FunctionDecl(info, found);
    }
    
    if (body != null)
      f.Body = body;
    return new FunctionDecl(info, f);
  }

  private Statement ParseFunction(TokenInfo info, bool isInComposite = false) => ParseFunction(info, () => Mangle(isInComposite ? SymbolType.CompositeInternal : SymbolType.GlobalDeclaration));

  private Statement ParseComposite(TokenInfo info, Composite.Type kind, Func<Composite, Statement> factory) {
    if (!InGlobalScope())
      Error($"{kind} cannot be outside of global scope");
    string ident = Mangle(SymbolType.GlobalDeclaration);
    
    if (TryConsume(Token.Get(Token.Type.SEMI)))
    {
      Composite ret = new(ident, [], [], kind);
      composites[ident] = ret;
      return factory(ret);
    }

    Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
    List<Statement> group = [];
    Composite s = Switch(body, () =>
    {
      Composite s = new(ident, [], [], kind);
      Context ctx = new CompositeContext(s);
      composites[ident] = s;
      currentContext.Push(ctx);
      while (HasPeek())
      {
        if (TryConsume(Token.Get(Token.Type.FUN)))
        {
          Statement func = ParseFunction(info, true);
          group.Add(func);
        }
        else
        {
          ModifierHandler modifiers = GetModifiers(handler => { if (!handler.IsStatic) handler.Mutable(); });

          DataType type = ParseType();
          
          bool isStatic = modifiers.IsStatic;

          Variable variable;
          if (isStatic)
          {
            string temp = Mangle(SymbolType.CompositeInternal);
            variable = new(modifiers, type, temp);
            if (s.Statics.Keys.Any(v => v.Name == variable.Name))
              Error($"{kind} static field {variable.Name} already exists");

            Expression? val = null;
            if (TryConsume(Token.Get(Token.Type.EQUALS_SYMBOL)))
              val = ParseExpression(variable.Type);
            s.Statics[variable] = val;
            AddVariable(variable);
          }
          else
          {
            string name = NoMangle();
            variable = new(modifiers, type, name);
            if (s.Fields.Any(v => v.Name == variable.Name))
              Error($"{kind} non-static field {variable.Name} already exists");
            s.Fields.Add(variable);
          }
          TryConsumeError(Token.Get(Token.Type.SEMI));
        }
      }
      currentContext.Pop();
      return s; 
    });
    group.Insert(0, factory(s));
    return new Group(info, [.. group]);
  }

  private Expression ParseExpression(DataType? required)
  {
    typeCheckerContext.Push(required);
    Expression ret = ParseExpression();
    typeCheckerContext.Pop();
    return ret;
  }

  protected Statement ParseExtern(TokenInfo info, Func<string> namingConvention)
  {
    if (TryConsume(Token.Get(Token.Type.VAR)))
    {
      ModifierHandler modifiers = GetModifiers(handler =>
      {
        if (handler.IsStatic)
          Error("Extern variable cannot be static");
      });
      DataType type = ParseType();
      string name = namingConvention();
      Variable variable = new(modifiers, type, name);
      AddVariable(variable);
      return new ExternVariable(info, variable);
    }
    else if (TryConsume(Token.Get(Token.Type.FUN)))
    {
      FunctionDecl s = (ParseFunction(info, namingConvention) as FunctionDecl)!;
      Function func = s.Func;
      if (func.Body != null)
        Error("Extern function cannot have a body");
      if (func.Modifiers.IsStatic)
        Error("Extern function cannot be static");
      return new ExternFunction(info, func);
    }
    Error($"Extern only accepts functions and variables");
    throw new UnreachableException();
  }

  protected static bool IsIntegerLiteral(Expression expression) => expression is LiteralExpr lit && 
    (lit.Lit is CharLiteral || lit.Lit is ByteLiteral || lit.Lit is ShortLiteral || lit.Lit is UShortLiteral ||
    lit.Lit is IntLiteral || lit.Lit is UIntLiteral || lit.Lit is LongLiteral || lit.Lit is ULongLiteral);
  
  protected static bool IsBinOperatorConstant(BinaryExpr.BinaryOp op) => op != BinaryExpr.BinaryOp.Equals && op != BinaryExpr.BinaryOp.NotEquals &&
    op != BinaryExpr.BinaryOp.Greater && op != BinaryExpr.BinaryOp.Less && op != BinaryExpr.BinaryOp.GreaterEqual && op != BinaryExpr.BinaryOp.LessEqual &&
    op != BinaryExpr.BinaryOp.And && op != BinaryExpr.BinaryOp.Or && op != BinaryExpr.BinaryOp.Assign;
  
  protected static bool IsEnumConstant(Expression expression, Dictionary<string, long> entries)
  {
    if (IsIntegerLiteral(expression))
      return true;
    if (expression is BinaryExpr bin && IsEnumConstant(bin.Left, entries) && IsEnumConstant(bin.Right, entries) && IsBinOperatorConstant(bin.Operator))
      return true;
    if (expression is UnaryExpression un && IsEnumConstant(un.Base, entries))
      return true;
    if (expression is IdentifierExpression id && entries.ContainsKey(id.Variable.Name))
      return true;
    return false;
  }

  protected static long ParseIntegerLiteral(Literal lit) => lit switch
  {
    CharLiteral c => Literal.ParseChar(c.Character),
    ByteLiteral b => b.Byte,
    ShortLiteral s => s.Short,
    UShortLiteral us => us.UShort,
    IntLiteral i => i.Int,
    UIntLiteral ui => ui.UInt,
    LongLiteral l => l.Long,
    ULongLiteral ul => (long) ul.ULong,
    _ => throw new Exception($"Invalid Integer literal {lit}")
  };

  protected long ParseConstantBinary(Expression left, Expression right, BinaryExpr.BinaryOp op, Dictionary<string, long> entries) => op switch {
    BinaryExpr.BinaryOp.Add => ParseEnumConstant(left, entries) + ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.Sub => ParseEnumConstant(left, entries) - ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.Mul => ParseEnumConstant(left, entries) * ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.Div => ParseEnumConstant(left, entries) / ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.Mod => ParseEnumConstant(left, entries) % ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.BitAnd => ParseEnumConstant(left, entries) & ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.BitOr => ParseEnumConstant(left, entries) | ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.BitXor => ParseEnumConstant(left, entries) ^ ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.Shl => ParseEnumConstant(left, entries) << (int)ParseEnumConstant(right, entries),
    BinaryExpr.BinaryOp.Shr => ParseEnumConstant(left, entries) >> (int)ParseEnumConstant(right, entries),
    
    _ => throw new Exception($"{left} {op} {right} is not a constant integer expression")
  };

  protected long ParseConstantUnary(Expression expr, UnaryExpression.UnaryOperator op, Dictionary<string, long> entries) => op switch {
    UnaryExpression.UnaryOperator.Minus => -ParseEnumConstant(expr, entries),
    UnaryExpression.UnaryOperator.BitNot => ~ParseEnumConstant(expr, entries),
    _ => throw new Exception($"{op} {expr} is not a constant integer expression")
  };

  protected long ParseEnumConstant(Expression expression, Dictionary<string, long> entries)
  {
    if (!IsEnumConstant(expression, entries))
      Error($"{expression} is not an enum compatible constant expression");

    if (IsIntegerLiteral(expression))
      return ParseIntegerLiteral((expression as LiteralExpr)!.Lit);
    if (expression is BinaryExpr bin)
      return ParseConstantBinary(bin.Left, bin.Right, bin.Operator, entries);
    if (expression is UnaryExpression unary)
      return ParseConstantUnary(unary.Base, unary.Operator, entries);
    if (expression is IdentifierExpression id)
      return entries[id.Variable.Name];

    throw new UnreachableException();
  }

  protected Statement ParseABI(TokenInfo info, string abi) => abi switch
  {
    "C" => ParseExtern(info, () => NoMangle()),

    _ => throw new Exception($"Unsupported ABI {abi}"),
  };
  
  protected void Wakeup(Token.Type token, bool consume, Func<TokenInfo, Statement> action)
  {
    processes.Add(new ParsingProcess(token, consume, action));
  }
  protected void Semi() => TryConsumeError(Token.Get(Token.Type.SEMI));

  protected readonly Stack<(List<Variable> vars, int saved)> saved_snapshots = [];

  protected void PushSnapshot()
  {
    if (InScope(out var ctx))
    {
      saved_snapshots.Push((ctx!.Locals, ctx.Locals.Count));
      return;
    }
    saved_snapshots.Push((globals, globals.Count));
  }
  protected void PopSnapshot()
  {
    (List<Variable> current, int saved) = saved_snapshots.Pop();
    current.RemoveRange(saved, current.Count - saved);
  }
}
