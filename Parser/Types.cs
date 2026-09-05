namespace Parser;

public abstract class DataType
{
  public abstract Expression GetNull();
  public virtual bool Matches<T>(out T? value) where T : DataType
  {
    value = this as T;
    return value is not null;
  }
  public bool Matches<T>() where T : DataType => Matches<T>(out _);
  public virtual bool CanAccept(DataType other)
  {
    if (ReferenceEquals(this, other))
      return true;
    if (other.Matches<AliasType>(out var alias))
      return this == other || CanAccept(alias!.Type);
    return this == other;
  }
  public static bool IsNumeric(DataType type) => (type.Matches<AliasType>(out var alias)  && IsNumeric(alias!.Type)) || type.Matches<ByteType>() || type.Matches<CharType>() || 
    type.Matches<UShortType>() || type.Matches<ShortType>() || type.Matches<UIntType>() || type.Matches<IntType>() || type.Matches<ULongType>() || 
    type.Matches<LongType>() || type.Matches<FloatType>() || type.Matches<DoubleType>();

  public static bool IsUnsigned(DataType type) => (type.Matches<AliasType>(out var alias) && IsUnsigned(alias!.Type)) || type.Matches<ByteType>() || 
    type.Matches<UShortType>() || type.Matches<UIntType>() || type.Matches<ULongType>();
  
  public static bool IsSigned(DataType type) => (type.Matches<AliasType>(out var alias) && IsSigned(alias!.Type)) || type.Matches<CharType>() || 
    type.Matches<ShortType>() || type.Matches<IntType>() || type.Matches<LongType>() || type.Matches<FloatType>() || type.Matches<DoubleType>();
}

public class ByteType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new ByteLiteral((byte)l);
  public static readonly DataType INSTANCE = new ByteType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned char)0");
  public override Expression GetNull() => NULL;
}
public class CharType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new CharLiteral(((char)l).ToString());
  public static readonly DataType INSTANCE = new CharType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(char)0");
  public override Expression GetNull() => NULL;
}
public class UShortType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new UShortLiteral((ushort)l);
  public static readonly DataType INSTANCE = new UShortType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned short)0");
  public override Expression GetNull() => NULL;
}
public class ShortType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new ShortLiteral((short)l);
  public static readonly DataType INSTANCE = new ShortType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(short)0");
  public override Expression GetNull() => NULL;

}
public class UIntType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new UIntLiteral((uint)l);
  public static readonly DataType INSTANCE = new UIntType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned int)0");
  public override Expression GetNull() => NULL;
}
public class IntType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new IntLiteral((int)l);
  public static readonly DataType INSTANCE = new IntType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(int)0");
  public override Expression GetNull() => NULL;
}
public class ULongType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new ULongLiteral((ulong)l);
  public static readonly DataType INSTANCE = new ULongType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "(unsigned long long)0");
  public override Expression GetNull() => NULL;
}
public class LongType : DataType
{
  public static readonly Func<long, Literal> Factory = l => new LongLiteral(l);
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

public class DynamicType : DataType
{
  public static readonly DataType INSTANCE = new DynamicType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "NULL");
  public override Expression GetNull() => NULL;
  public override bool CanAccept(DataType other)
  {
    if (ReferenceEquals(this, other))
      return true;

    if (other.Matches<ArrayType>() || other.Matches<PointerType>() || other.Matches<FunctionType>() || other.Matches<StringType>())
      return true;
    
    return base.CanAccept(other);
  }
}

public class StringType : DataType
{
  public static readonly DataType INSTANCE = new StringType();
  public static readonly Expression NULL = new RawExpr(INSTANCE, "NULL");
  public override Expression GetNull() => NULL;
}

public class ArrayType(DataType elements, Expression? size) : DataType
{
  public DataType Elements {get;} = elements;
  public Expression? Size {get; set;} = size;
  public override Expression GetNull() => new RawExpr(this, "{0}");

  public override bool CanAccept(DataType other)
  {
    if (other.Matches<AliasType>(out var alias))
      return this == other || CanAccept(alias!.Type);
    return other.Matches<ArrayType>(out var arr) && Elements.CanAccept(arr!.Elements);
  }
}
public class PointerType(DataType target, bool mutable) : DataType
{
  public DataType Target {get;} = target;
  public bool Mutable {get;} = mutable;
  public override Expression GetNull() => new RawExpr(this, "NULL");
  public override bool CanAccept(DataType other)
  {
    if (ReferenceEquals(this, other))
      return true;
    if (other.Matches<ArrayType>() || other.Matches<PointerType>() || other.Matches<FunctionType>() || other.Matches<DynamicType>() || other.Matches<StringType>())
      return true;
    return base.CanAccept(other);
  }
}
public class FunctionType(DataType? Result, DataType[] Args, bool Variadic) : DataType
{
  public DataType? Return {get;} = Result;
  public DataType[] Arguments {get;} = Args;
  public bool Variadic {get;} = Variadic;
  public override Expression GetNull() => new RawExpr(this, "NULL");
}

public class AliasType(DataType Target, string Name) : DataType
{
  public DataType Type {get;} = Target;
  public string Alias {get;} = Name;
  public override Expression GetNull() => Type.GetNull();
  public override bool Matches<T>(out T? value) where T : class
  {
    if (this is T cast)
    {
      value = cast;
      return true;
    }
    return Type.Matches(out value);
  }
  public override bool CanAccept(DataType other) => base.CanAccept(other) || Type.CanAccept(other);
}

public class CompositeType(Composite Comp) : DataType
{
  public Composite Comp {get;} = Comp;
  public override Expression GetNull() => new RawExpr(this, "{0}");
}

public static class References {
  private static readonly List<PointerType> PointerCache = [];
  private static readonly Dictionary<string, AliasType> AliasCache = [];
  private static readonly Dictionary<string, CompositeType> StructCache = [];
  private static readonly List<FunctionType> FunctionCache = [];

  public static DataType GetPointerType(DataType target, bool mutable)
  {
    PointerType? value = PointerCache.Find(p => p.Target == target && p.Mutable == mutable);
    if (value == null)
    {
      value = new PointerType(target, mutable);
      PointerCache.Add(value);
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

  public static DataType GetFunctionType(DataType? result, DataType[] args, bool variadic)
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
      found = new FunctionType(result, args, variadic);
      FunctionCache.Add(found);
    }
    return found;
  }
}
