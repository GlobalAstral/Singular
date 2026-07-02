namespace Parser;

public class ClassContext(ModifierHandler modifiers, string name, string[] generics)
{
  public ModifierHandler Modifiers = modifiers;
  public string Name = name;
  public readonly string[] Generics = generics;
  public readonly List<Variable> Fields = [];
  public readonly List<Function> Functions = [];
}
