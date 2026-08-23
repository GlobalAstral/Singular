namespace Parser;

public class Variable(ModifierHandler Modifiers, DataType Type, string Name)
{
  public ModifierHandler Modifiers {get;} = Modifiers;
  public DataType Type {get;} = Type;
  public string Name {get;} = Name;

  public override string ToString() => $"{Modifiers} {Type} {Name}";
}
