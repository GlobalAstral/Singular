
namespace Parser;

public struct Composite(string Name, Variable[] Fields, Dictionary<Variable, Expression?> Statics, Composite.Type type)
{
  public enum Type
  {
    STRUCT,
    UNION
  }
  public Type Kind {get;} = type;
  public string Name {get;} = Name;
  public List<Variable> Fields {get;} = [.. Fields];
  public Dictionary<Variable, Expression?> Statics {get;} = Statics;
}
