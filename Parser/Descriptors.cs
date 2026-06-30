using System.Diagnostics.CodeAnalysis;

namespace Parser;

public struct Field(string name, DataType type, ModifierHandler handler, Expression? expression)
{
  public string Name = name;
  public DataType type = type;
  public ModifierHandler modifiers = handler;
  public Expression? expression = expression;
  public override bool Equals(object? obj)
  {
    throw new NotImplementedException();
  }

  public static bool operator ==(Field a, Field b) => a.Equals(b);
  public static bool operator !=(Field a, Field b) => !a.Equals(b);

  public override int GetHashCode()
  {
    throw new NotImplementedException();
  }
}

public struct Function
{
  public override bool Equals([NotNullWhen(true)] object? obj)
  {
    throw new NotImplementedException();
  }

  public static bool operator ==(Function a, Function? b) => a.Equals(b);
  public static bool operator !=(Function a, Function? b) => !a.Equals(b);

  public override int GetHashCode()
  {
    throw new NotImplementedException();
  }
}

public class ClassDescriptor(ModifierHandler modifiers, string name)
{
  protected ModifierHandler Modifiers = modifiers;
  protected string Name = name;
  protected Field[] Fields = [];
  protected Function[] Functions = [];
  protected string[] Generics = [];

  public override bool Equals(object? obj) => obj is ClassDescriptor other && Name == other.Name;
  public static bool operator ==(ClassDescriptor a, ClassDescriptor b) => a.Equals(b);
  public static bool operator !=(ClassDescriptor a, ClassDescriptor b) => !a.Equals(b);

  public override int GetHashCode() => HashCode.Combine(Modifiers, Name, Fields, Functions, Generics);
}
