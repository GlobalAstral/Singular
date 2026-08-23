namespace Parser;

public interface Context { }

public class FunctionContext(DataType? retType, Variable[] arguments) : Context
{
  public DataType? ReturnType {get;} = retType;
  public Variable[] Arguments {get;} = arguments;
}

public class StructContext(Struct @struct) : Context
{
  public Struct Struct {get;} = @struct;
}
