namespace Parser;

public abstract class DataType
{
  public abstract Expression GetNull();
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
  public static readonly Expression NULL = new RawExpr(INSTANCE, "0.0f");
  public override Expression GetNull() => NULL;

}
public class DoubleType : DataType
{
  public static readonly DataType INSTANCE = new DoubleType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "0.0");
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

public static class References {
  private static readonly List<ArrayType> ArrayCache = [];
  private static readonly List<PointerType> PointerCache = [];
  private static readonly List<FunctionType> FunctionCache = [];
  private static readonly List<AliasType> AliasCache = [];
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

  public static DataType GetAliasType(string name, DataType target)
  {
    AliasType? found = AliasCache.Find(ele => ele.Alias == name && ele.Type == target);
    if (found is null)
    {
      found = new AliasType(target, name);
      AliasCache.Add(found);
    }
    return found;
  }
}
