
namespace Parser;

public struct Struct(string Name, Variable[] Fields, Dictionary<Variable, Expression?> Statics)
{
  public string Name {get;} = Name;
  public List<Variable> Fields {get;} = [.. Fields];
  public Dictionary<Variable, Expression?> Statics {get;} = Statics;
}
