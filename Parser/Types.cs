namespace Parser;

public abstract class DataType { }

public class ByteType : DataType
{
  public static readonly DataType INSTANCE = new ByteType();
}
public class CharType : DataType
{
  public static readonly DataType INSTANCE = new CharType();
}
public class UShortType : DataType
{
  public static readonly DataType INSTANCE = new UShortType();
}
public class ShortType : DataType
{
  public static readonly DataType INSTANCE = new ShortType();
}
public class UIntType : DataType
{
  public static readonly DataType INSTANCE = new UIntType();
}
public class IntType : DataType
{
  public static readonly DataType INSTANCE = new IntType();
}
public class ULongType : DataType
{
  public static readonly DataType INSTANCE = new ULongType();
}
public class LongType : DataType
{
  public static readonly DataType INSTANCE = new LongType();
}
public class BooleanType : DataType
{
  public static readonly DataType INSTANCE = new BooleanType();
}
public class FloatType : DataType
{
  public static readonly DataType INSTANCE = new FloatType();
}
public class DoubleType : DataType
{
  public static readonly DataType INSTANCE = new DoubleType();
}

public class ArrayType(DataType elements) : DataType
{
  public DataType Elements {get;} = elements;
}
public class PointerType(DataType target) : DataType
{
  public DataType Target {get;} = target;
}
public class FunctionType(DataType? Result, DataType[] Args) : DataType
{
  public DataType? Return {get;} = Result;
  public DataType[] Arguments {get;} = Args;
}
public class ObjectType(ClassDescriptor @class, DataType[] generics) : DataType
{
  public ClassDescriptor Class {get;} = @class;
  public DataType[] Generics {get;} = generics;
}

public static class References {
  private static readonly List<ArrayType> ArrayCache = [];
  private static readonly List<PointerType> PointerCache = [];
  private static readonly List<FunctionType> FunctionCache = [];
  private static readonly List<ObjectType> ObjectCache = [];

  public static DataType GetArrayType(DataType elements)
  {
    ArrayType? found = ArrayCache.Find(ele => ele.Elements == elements);
    if (found is null)
    {
      found = new ArrayType(elements);
      ArrayCache.Add(found);
    }
    return found;
  }

  public static DataType GetPointerType(DataType target)
  {
    PointerType? found = PointerCache.Find(ele => ele.Target == target);
    if (found is null)
    {
      found = new PointerType(target);
      PointerCache.Add(found);
    }
    return found;
  }

  public static DataType GetFunctionType(DataType? result, DataType[] args)
  {
    FunctionType? found = FunctionCache.Find(ele =>
    {
      if (result != ele.Return)
        return false;
      if (args.Length != ele.Arguments.Length)
        return false;
      for (int i = 0; i < args.Length; i++)
      {
        if (args[i] != ele.Arguments[i])
          return false;
      }
      return true;
    });

    if (found is null)
    {
      found = new FunctionType(result, args);
      FunctionCache.Add(found);
    }
    return found;
  }

  public static DataType GetObjectType(ClassDescriptor @class, DataType[] generics)
  {
    ObjectType? found = ObjectCache.Find(ele =>
    {
      if (@class != ele.Class)
        return false;
      if (generics.Length != ele.Generics.Length)
        return false;
      for (int i = 0; i < generics.Length; i++)
      {
        if (generics[i] != ele.Generics[i])
          return false;
      }
      return true;
    });

    if (found is null)
    {
      found = new ObjectType(@class, generics);
      ObjectCache.Add(found);
    }
    return found;
  }
}
