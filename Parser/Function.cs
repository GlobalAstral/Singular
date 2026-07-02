namespace Parser;

public record Function(ModifierHandler Modifiers, string Name, Variable[] Arguments, DataType ReturnType, SyntaxInstance? Body)
{
  
}
