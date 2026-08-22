namespace Parser;

public class Function(ModifierHandler modifiers, string name, Variable[] arguments, DataType? returnType, Statement? body)
{
  public ModifierHandler Modifiers {get;} = modifiers;
  public string Name {get;} = name;
  public Variable[] Arguments {get;} = arguments;
  public DataType? ReturnType {get;} = returnType;
  public Statement? Body {get;} = body;

  public override bool Equals(object? obj)
  { 
    if (obj == null || GetType() != obj.GetType())
      return false;

    Function f = (Function)obj;    
    return Name == f.Name;
  }

  public override int GetHashCode() => Name.GetHashCode();
}
