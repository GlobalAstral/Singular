
namespace Parser;

public interface Expression
{
  public DataType GetReturnType();
}

public class LiteralExpr(Literal Lit) : Expression
{
  public Literal Lit {get;} = Lit;

  public DataType GetReturnType() => Lit.GetReturnType();
}
