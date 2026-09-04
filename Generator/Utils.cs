using System.Diagnostics;
using System.Text;
using Parser;

namespace Generator;

public partial class Generator
{
  protected string Indent() => new('\t', indentLevel);
  protected string NewLine() => $"\n{Indent()}";
  protected string GenerateGroup(Statement[] stmts) => Switch(stmts, () =>
    {
      contexts.Push(new Context());
      string ret = string.Join(NewLine(), Process());
      Context current = contexts.Pop();
      ret = ret.Insert(0, current.Prologue());
      return ret;
    });
  protected string GenerateScope(Statement[] stmts)
  {
    StringBuilder builder = new();
    builder.Append('{');
    indentLevel++;
    builder.Append(NewLine());
    builder.Append(GenerateGroup(stmts));
    indentLevel--;
    builder.Append(NewLine());
    builder.Append('}');
    return builder.ToString();
  }

  protected string GenerateTypedefName() => $"T{typedefID++}_{RandomizedParserHash}";

  protected string GenerateArrayType(DataType type, Expression size)
  {
    string typedef_name = GenerateTypedefName();
    string typedef = $"typedef {GenerateType(type)} {typedef_name}[{GenerateExpression(size)}];\n";
    contexts.Peek()!.Prologue(typedef);
    return typedef_name;
  }

  protected string GenerateFunctionType(DataType? returnType, DataType[] args, bool variadic)
  {
    string typedef_name = GenerateTypedefName();
    string retType = returnType == null ? "void" : GenerateType(returnType);
    string typedef = $"typedef {retType} (*{typedef_name})({string.Join(", ", args.Select(t => GenerateType(t)))});\n";
    contexts.Peek()!.Prologue(typedef);
    return typedef_name;
  }

  protected string GenerateType(DataType dataType) => dataType switch
  {
    ByteType => "unsigned char",
    CharType => "char",
    UShortType => "unsigned short",
    ShortType => "short",
    UIntType => "unsigned int",
    IntType => "int",
    ULongType => "unsigned long long",
    LongType => "long long",
    BooleanType => "bool",
    FloatType => "float",
    DoubleType => "double",
    DynamicType => "void*",
    StringType => "char*",
    ArrayType arr => GenerateArrayType(arr.Elements, arr.Size),
    PointerType ptr => $"{GenerateType(ptr.Target)}{(!ptr.Mutable ? " const" : "")}*",
    FunctionType fn => GenerateFunctionType(fn.Return, fn.Arguments, fn.Variadic),
    AliasType alias => alias.Alias,
    CompositeType comp => comp.Comp.Name,
    _ => throw new UnreachableException()
  };

  protected string GenerateCompositeLiteral(CompositeType composite, Dictionary<string, Expression> values)
    => $"({composite.Comp.Name}) {{{string.Join(", ", values.Select(pair => $".{pair.Key} = {GenerateExpression(pair.Value)}"))}}}";

  protected string GenerateLambdaName() => $"L{lambdaID++}{RandomizedParserHash}";
  protected string GenerateLambda(Lambda lambda)
  {
    string lambdaName = GenerateLambdaName();
    string func = GenerateFunctionDeclaration(new(new ModifierHandler().Static(), lambdaName, lambda.Arguments, lambda.ReturnType, lambda.Body, lambda.Variadic));
    contexts.Peek()!.Prologue(func);
    return lambdaName;
  }
  protected string GenerateUnary(UnaryExpression.UnaryOperator op, Expression expr) => op switch
  {
    UnaryExpression.UnaryOperator.Minus => $"-{GenerateExpression(expr)}",
    UnaryExpression.UnaryOperator.Not => $"!{GenerateExpression(expr)}",
    UnaryExpression.UnaryOperator.BitNot => $"~{GenerateExpression(expr)}",
    UnaryExpression.UnaryOperator.PreInc => $"++{GenerateExpression(expr)}",
    UnaryExpression.UnaryOperator.PreDec => $"--{GenerateExpression(expr)}",
    UnaryExpression.UnaryOperator.Deref => $"*{GenerateExpression(expr)}",
    UnaryExpression.UnaryOperator.Ref or UnaryExpression.UnaryOperator.MutRef => $"&{GenerateExpression(expr)}",
    UnaryExpression.UnaryOperator.Sizeof => $"sizeof({GenerateExpression(expr)})",
    _ => throw new UnreachableException()
  };

  protected string GenerateBinary(Expression left, Expression right, BinaryExpr.BinaryOp op) => op switch
  {
    BinaryExpr.BinaryOp.Add => $"{GenerateExpression(left)} + {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Sub => $"{GenerateExpression(left)} + {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Mul => $"{GenerateExpression(left)} * {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Div => $"{GenerateExpression(left)} / {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Mod => $"{GenerateExpression(left)} % {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.BitAnd => $"{GenerateExpression(left)} & {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.BitOr => $"{GenerateExpression(left)} | {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.BitXor => $"{GenerateExpression(left)} ^ {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Shl => $"{GenerateExpression(left)} << {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Shr => $"{GenerateExpression(left)} >> {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Equals => $"{GenerateExpression(left)} == {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.NotEquals => $"{GenerateExpression(left)} != {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Greater => $"{GenerateExpression(left)} > {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Less => $"{GenerateExpression(left)} < {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.GreaterEqual => $"{GenerateExpression(left)} >= {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.LessEqual => $"{GenerateExpression(left)} <= {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.And => $"{GenerateExpression(left)} && {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Or => $"{GenerateExpression(left)} || {GenerateExpression(right)}",
    BinaryExpr.BinaryOp.Assign => $"{GenerateExpression(left)} = {GenerateExpression(right)}",
    _ => throw new UnreachableException()
  };

  protected string GenerateExpression(Expression expression) => $"({GenerateExpression_(expression)})";
  protected string GenerateExpression_(Expression expression) => expression switch
  {
    LiteralExpr litexpr => litexpr.Lit.ToString()!,
    RawExpr raw => raw.Generated,
    IdentifierExpression id => id.Variable.Name,
    ArrayLiteral arr => $"{{{string.Join(", ", arr.Expressions.Select(v => GenerateExpression(v)))}}}",
    CompositeLiteral cmp => GenerateCompositeLiteral((CompositeType) cmp.Type, cmp.Expressions),
    FunctionPointer fptr => fptr.Function.Name,
    Lambda lambda => GenerateLambda(lambda),
    UnaryExpression unary => GenerateUnary(unary.Operator, unary.Base),
    MemberAccess memberAccess => $"{GenerateExpression(memberAccess.Expression)}{(memberAccess.Expression.GetReturnType() is PointerType ? "->" : ".")}{memberAccess.Field.Name}",
    IndexExpr index => $"{GenerateExpression(index.Base)}[{GenerateExpression(index.Index)}]",
    FunctionCall fn => $"{GenerateExpression(fn.Base)}({string.Join(", ", fn.Args.Select(a => GenerateExpression(a)))})",
    Cast cast => $"({GenerateType(cast.Type)}) {GenerateExpression(cast.Base)}",
    BitCast bcast => $"*(({GenerateType(bcast.Type)}*) &{GenerateExpression(bcast.Base)})",
    TernaryOperator ternary => $"({GenerateExpression(ternary.Condition)}) ? {GenerateExpression(ternary.Success)} : {GenerateExpression(ternary.Fail)}",
    PostIncrement post => $"{GenerateExpression(post.Base)}{(post.Direction > 0 ? "++" : "--")}",
    BinaryExpr bin => GenerateBinary(bin.Left, bin.Right, bin.Operator),
    _ => throw new UnreachableException()
  };

  protected static string GenerateModifiers(ModifierHandler modifiers)
  {
    StringBuilder builder = new();
    if (modifiers.IsStatic)
      builder.Append("static");
    if (!modifiers.IsMutable)
      builder.Append(" const");
    return builder.ToString();
  }

  protected string GenerateVariable(Variable variable) => $"{GenerateModifiers(variable.Modifiers)} {GenerateType(variable.Type)} {variable.Name}";

  protected string GenerateFunctionDeclaration(Function func)
  {
    string retType = func.ReturnType != null ? GenerateType(func.ReturnType) : "void";
    string sig = $"{GenerateModifiers(func.Modifiers)} {retType} {func.Name}({string.Join(", ", func.Arguments.Select(v => GenerateVariable(v)))}{(func.Variadic ? ", ..." : "")})";
    if (func.Body == null)
      return $"{sig};";
    Statement[] temp = [func.Body];
    string body = Switch(temp, ProcessOne);
    return $"{sig}{body}";
  }

  protected string GenerateReturn(Expression? expression) => $"return{(expression != null ? $" {GenerateExpression(expression)}" : "")};";

  protected string GenerateVarDecl(Variable variable, Expression? expr) => $"{GenerateVariable(variable)}{(expr == null ? "" : $" = {GenerateExpression(expr)}")};";

  protected string GenerateStructDecl(Composite @struct)
  {
    indentLevel++;
    string ret = $$"""
      typedef struct {{@struct.Name}} {
        {{string.Join($";{NewLine()}", @struct.Fields.Select(f => GenerateVariable(f)))}}
      } {{@struct.Name}};
      {{string.Join(";\n", @struct.Statics.Select(f => GenerateVarDecl(f.Key, f.Value)))}}
    """;
    indentLevel--;
    return ret;
  }

  protected string GenerateUnionDecl(Composite @union)
  {
    string statics = $$"""{{string.Join($";{NewLine()}", @union.Statics.Select(f => GenerateVarDecl(f.Key, f.Value)))}}""";
    indentLevel++;
    string ret = $$"""
      typedef union {{@union.Name}} {
        {{string.Join($";{NewLine()}", @union.Fields.Select(f => GenerateVariable(f)))}}
      } {{@union.Name}};
      {{statics}}
    """;
    indentLevel--;
    return ret;
  }

  protected string GenerateTypedef(DataType dataType, string name) => $"typedef {GenerateType(dataType)} {name};";

  protected string GenerateIf(Expression condition, Statement success, Statement? fail) => $$"""
    if ({{GenerateExpression(condition)}}) {{Switch([success], ProcessOne)}}
    {{(fail == null ? "" : $"else {Switch([fail], ProcessOne)}")}}
  """;

  protected string GenerateWhile(Expression condition, Statement body) => $$"""
    while ({{GenerateExpression(condition)}}) {{Switch([body], ProcessOne)}}
  """;

  protected string GenerateDoWhile(Expression condition, Statement body) => $$"""
    do {
      {{Switch([body], ProcessOne)}}
    } while ({{GenerateExpression(condition)}});
  """;

  protected string GenerateForLoop(Statement init, Expression condition, Statement update, Statement body) => $$"""
    for ({{Switch([init], ProcessOne)}}; {{GenerateExpression(condition)}}; {{Switch([update], ProcessOne)}}) {{Switch([body], ProcessOne)}}
  """;

  protected string GenerateSwitch(Expression Expression, (Expression, Statement)[] Cases, Statement? Default)
  {
    indentLevel++;
    string def = Default == null ? "" : $"default: {Switch([Default], ProcessOne)}";
    string ret = $$"""
      switch ({{GenerateExpression(Expression)}}) {
        {{string.Join(NewLine(), Cases.Select(pair => $"case {GenerateExpression(pair.Item1)}: {Switch([pair.Item2], ProcessOne)}"))}}
        {{def}}
      }
    """;
    indentLevel--;
    return ret;
  }
}
