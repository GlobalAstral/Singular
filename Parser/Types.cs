namespace Parser;

public abstract class DataType
{
  public abstract Expression GetNull();
  public static bool IsNumeric(DataType type) => (type is AliasType alias && IsNumeric(alias.Type)) || type is ByteType || type is CharType || 
    type is UShortType || type is ShortType || type is UIntType || type is IntType || type is ULongType || type is LongType || type is FloatType || 
    type is DoubleType;

  public static bool IsUnsigned(DataType type) => (type is AliasType alias && IsUnsigned(alias.Type)) || type is ByteType || type is UShortType || 
    type is UIntType || type is ULongType;
  
  public static bool IsSigned(DataType type) => (type is AliasType alias && IsSigned(alias.Type)) || type is CharType || type is ShortType || 
    type is IntType || type is LongType || type is FloatType || type is DoubleType;
}

public class ByteType : DataType
{
  public static readonly DataType INSTANCE = new ByteType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned char)0");
  public override Expression GetNull() => NULL;
}
public class CharType : DataType
{
  public static readonly DataType INSTANCE = new CharType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(char)0");
  public override Expression GetNull() => NULL;
}
public class UShortType : DataType
{
  public static readonly DataType INSTANCE = new UShortType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned short)0");
  public override Expression GetNull() => NULL;
}
public class ShortType : DataType
{
  public static readonly DataType INSTANCE = new ShortType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(short)0");
  public override Expression GetNull() => NULL;

}
public class UIntType : DataType
{
  public static readonly DataType INSTANCE = new UIntType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned int)0");
  public override Expression GetNull() => NULL;

}
public class IntType : DataType
{
  public static readonly DataType INSTANCE = new IntType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(int)0");
  public override Expression GetNull() => NULL;

}
public class ULongType : DataType
{
  public static readonly DataType INSTANCE = new ULongType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned long long)0");
  public override Expression GetNull() => NULL;

}
public class LongType : DataType
{
  public static readonly DataType INSTANCE = new LongType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(long long)0");
  public override Expression GetNull() => NULL;

}
public class BooleanType : DataType
{
  public static readonly DataType INSTANCE = new BooleanType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "false");
  public override Expression GetNull() => NULL;

}
public class FloatType : DataType
{
  public static readonly DataType INSTANCE = new FloatType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(float)0.0");
  public override Expression GetNull() => NULL;

}
public class DoubleType : DataType
{
  public static readonly DataType INSTANCE = new DoubleType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(double)0.0");
  public override Expression GetNull() => NULL;

}

public class ArrayType(DataType elements) : DataType
{
  public DataType Elements {get;} = elements;

  public override Expression GetNull() => new RawExpr(this, "{0}");
}
public class PointerType(DataType target) : DataType
{
  public DataType Target {get;} = target;
  public override Expression GetNull() => new RawExpr(this, "NULL");
}
public class FunctionType(DataType? Result, DataType[] Args) : DataType
{
  public DataType? Return {get;} = Result;
  public DataType[] Arguments {get;} = Args;
  public override Expression GetNull() => new RawExpr(this, "NULL");
  
}

public class AliasType(DataType Target, string Name) : DataType
{
  public DataType Type {get;} = Target;
  public string Alias {get;} = Name;
  public override Expression GetNull() => Type.GetNull();
}

public class CompositeType(Composite Comp) : DataType
{
  public Composite Comp {get;} = Comp;
  public override Expression GetNull() => new RawExpr(this, "{0}");
}

public static class References {
  private static readonly Dictionary<DataType, ArrayType> ArrayCache = [];
  private static readonly Dictionary<DataType, PointerType> PointerCache = [];
  private static readonly Dictionary<string, AliasType> AliasCache = [];
  private static readonly Dictionary<string, CompositeType> StructCache = [];
  private static readonly List<FunctionType> FunctionCache = [];
  public static DataType GetArrayType(DataType elements)
  {
    if (!ArrayCache.TryGetValue(elements, out var value))
    {
      value = new ArrayType(elements);
      ArrayCache[elements] = value;
    }
    return value;
  }

  public static DataType GetPointerType(DataType target)
  {
    if (!PointerCache.TryGetValue(target, out var value))
    {
      value = new PointerType(target);
      PointerCache[target] = value;
    }
    return value;
  }

  public static DataType GetAliasType(string name, DataType target)
  {
    if (!AliasCache.TryGetValue(name, out var value))
    {
      value = new AliasType(target, name);
      AliasCache[name] = value;
    }
    return value;
  }

  public static DataType GetCompositeType(string name, Composite @struct)
  {
    if (!StructCache.TryGetValue(name, out var value))
    {
      value = new CompositeType(@struct);
      StructCache[name] = value;
    }
    return value;
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
}
