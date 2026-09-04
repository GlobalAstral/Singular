using Lexer;
using Parser;

namespace Generator;

public partial class Generator(Statement[] statements, int hash) : Processor<Statement, string>(statements, s => s.Trim().Length == 0)
{
  protected int indentLevel = 0;
  protected Stack<Context> contexts = [];
  protected uint typedefID = 0;
  protected uint lambdaID = 0;
  protected static readonly double RandomDouble = new Random().NextDouble();
  protected readonly int RandomizedParserHash = HashCode.Combine(hash, RandomDouble);

  protected string GenerateLineDirective(TokenInfo info) => $"{NewLine()}#line {info.Line} \"{info.File}\"{NewLine()}";

  public override string ProcessOne() {
    Statement stmt = Consume();
    
    TokenInfo info = stmt.GetInfo();
    string line = info.Line > 0 ? GenerateLineDirective(info) : "";

    string res = stmt switch
    {
      Group group => GenerateGroup(group.Content),
      Scope scope => GenerateScope(scope.Content),
      FunctionDecl fn => GenerateFunctionDeclaration(fn.Func),
      Return ret => GenerateReturn(ret.expr),
      StructDecl @struct => GenerateStructDecl(@struct.Struct),
      UnionDecl @union => GenerateUnionDecl(@union.Union),
      VariableDecl vardecl => GenerateVarDecl(vardecl.Variable, vardecl.Expression),
      TypeDefinition typedef => GenerateTypedef(typedef.Type, typedef.Name),
      IgnoredExpr ignoredExpr => $"{GenerateExpression(ignoredExpr.Expression)};",
      IfStatement ifstmt => GenerateIf(ifstmt.Expression, ifstmt.Statement, ifstmt.Else),
      WhileStmt whileStmt => GenerateWhile(whileStmt.Expression, whileStmt.Body),
      DoWhileStmt doWhileStmt => GenerateDoWhile(doWhileStmt.Expression, doWhileStmt.Body),
      ForStmt forStmt => GenerateForLoop(forStmt.Init, forStmt.Condition, forStmt.Update, forStmt.Body),
      BreakStmt => "break;",
      ContinueStmt => "continue;",
      SwitchStmt switchStmt => GenerateSwitch(switchStmt.Expression, switchStmt.Cases, switchStmt.Default),
      Nop => "",
      ExternVariable exvar => $"extern {GenerateVarDecl(exvar.Variable, null)}",
      ExternFunction exfun => $"extern {GenerateFunctionDeclaration(exfun.Function)}",
      _ => throw new NotImplementedException(),
    };

    return $"{line}{res}";
  }

  public override string[] Process()
  {
    contexts.Push(new Context());
    List<string> ret = [.. base.Process()];
    Context current = contexts.Pop();
    ret.Insert(0, current.Prologue());
    return [.. ret];
  }
}
