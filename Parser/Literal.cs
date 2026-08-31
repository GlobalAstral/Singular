using System.Reflection.PortableExecutable;

namespace Parser;

public interface Literal
{
  public static Literal ParseLiteral(string lit)
  {
    if (lit == "true" || lit == "false")
      return new BooleanLiteral(lit == "true");
    
    if (lit.StartsWith('\'') && lit.EndsWith('\''))
      return new CharLiteral(lit[1..^1]);

    if (lit.StartsWith('"') && lit.EndsWith('"'))
      return new StringLiteral(lit[1..^1]);

    char? suffix = null;
    if (lit.EndsWith('l') || lit.EndsWith('f') || lit.EndsWith('h'))
    {
      suffix = lit.Last();
      lit = lit[..^1];
    }

    bool unsigned = false;
    if (lit.EndsWith('u'))
    {
      unsigned = true;
      lit = lit[..^1];
    }

    if (lit.Contains('.'))
    {
      if (suffix != null && suffix != 'f')
        throw new Exception($"Invalid literal suffix for double {suffix}. Expected 'f' or nothing");
      if (unsigned)
        throw new Exception("Floating point literal cannot be unsigned");
      if (suffix == 'f')
        return new FloatLiteral(float.Parse(lit));
      return new DoubleLiteral(double.Parse(lit));      
    }
    
    bool hex = false;
    if (lit.StartsWith("0x"))
    {
      hex = true;
      lit = lit[2..];
    }

    if (suffix == 'l')
    {
      if (unsigned)
        return new ULongLiteral(hex ? ulong.Parse(lit, System.Globalization.NumberStyles.HexNumber) : ulong.Parse(lit));
      return new LongLiteral( hex ? long.Parse(lit, System.Globalization.NumberStyles.HexNumber) : long.Parse(lit) );
    }
    if (suffix == 'h')
    {
      if (unsigned)
        return new UShortLiteral(hex ? ushort.Parse(lit, System.Globalization.NumberStyles.HexNumber) : ushort.Parse(lit));
      return new ShortLiteral( hex ? short.Parse(lit, System.Globalization.NumberStyles.HexNumber) : short.Parse(lit) );
    }
    if (suffix == 'f')
    {
      if (unsigned)
        throw new Exception("Floating point literal cannot be unsigned");
      return new FloatLiteral( hex ? throw new Exception("Float suffic cannot be used with HEX literals") : float.Parse(lit) );
    }
    if (unsigned)
      return new UIntLiteral( hex ? uint.Parse(lit, System.Globalization.NumberStyles.HexNumber) : uint.Parse(lit) );
    return new IntLiteral(hex ? int.Parse(lit, System.Globalization.NumberStyles.HexNumber) : int.Parse(lit));
  }
  public DataType GetReturnType();
}

public readonly struct CharLiteral(string Char) : Literal
{
  public string Character {get;} = Char;
  public readonly DataType GetReturnType() => CharType.INSTANCE;
  public override string ToString() => $"'{Character}'";
}

public readonly struct ShortLiteral(short Short) : Literal
{
  public short Short {get;} = Short;
  public readonly DataType GetReturnType() => ShortType.INSTANCE;
  public override string ToString() => $"{Short}h";
}

public readonly struct IntLiteral(int Int) : Literal
{
  public int Int {get;} = Int;
  public readonly DataType GetReturnType() => IntType.INSTANCE;
  public override string ToString() => $"{Int}";
}

public readonly struct LongLiteral(long Long) : Literal
{
  public long Long {get;} = Long;
  public readonly DataType GetReturnType() => LongType.INSTANCE;
  public override string ToString() => $"{Long}l";
}

public readonly struct UShortLiteral(ushort UShort) : Literal
{
  public ushort UShort {get;} = UShort;
  public readonly DataType GetReturnType() => UShortType.INSTANCE;
  public override string ToString() => $"{UShort}h";
}

public readonly struct UIntLiteral(uint UInt) : Literal
{
  public uint UInt {get;} = UInt;
  public readonly DataType GetReturnType() => UIntType.INSTANCE;
  public override string ToString() => $"{UInt}";
}

public readonly struct ULongLiteral(ulong ULong) : Literal
{
  public ulong ULong {get;} = ULong;
  public readonly DataType GetReturnType() => ULongType.INSTANCE;
  public override string ToString() => $"{ULong}l";
}

public readonly struct BooleanLiteral(bool Boolean) : Literal
{
  public bool Boolean {get;} = Boolean;
  public readonly DataType GetReturnType() => BooleanType.INSTANCE;
  public override string ToString() => $"{Boolean}";
}

public readonly struct FloatLiteral(float Float) : Literal
{
  public float Float {get;} = Float;
  public readonly DataType GetReturnType() => FloatType.INSTANCE;
  public override string ToString() => $"{Float}f";
}

public readonly struct DoubleLiteral(double Double) : Literal
{
  public double Double {get;} = Double;
  public readonly DataType GetReturnType() => DoubleType.INSTANCE;
  public override string ToString() => $"{Double}";
}

public readonly struct StringLiteral(string String) : Literal
{
  public string String {get;} = String;
  public readonly DataType GetReturnType() => StringType.INSTANCE;
  public override string ToString() => $"\"{String}\"";
}
