using System.Globalization;
using System.Numerics;

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
      suffix = lit[^1];
      lit = lit[..^1];
    }

    bool unsigned = false;

    if (lit.EndsWith('u'))
    {
      unsigned = true;
      lit = lit[..^1];
    }

    // Floating point
    if (lit.Contains('.'))
    {
      if (suffix != null && suffix != 'f')
        throw new Exception($"Invalid literal suffix for floating-point literal: {suffix}. Expected 'f' or nothing");
      if (unsigned)
        throw new Exception("Floating-point literal cannot be unsigned");
      if (suffix == 'f')
        return new FloatLiteral(float.Parse(lit, CultureInfo.InvariantCulture));
      return new DoubleLiteral(double.Parse(lit, CultureInfo.InvariantCulture));
    }

    // `f` without a decimal point is still a float.
    if (suffix == 'f')
    {
      if (unsigned)
        throw new Exception("Floating-point literal cannot be unsigned");
      if (lit.StartsWith("0x") || lit.StartsWith("0b"))
        throw new Exception("Float suffix cannot be used with hexadecimal or binary literals");
      return new FloatLiteral(float.Parse(lit, CultureInfo.InvariantCulture));
    }

    int radix = 10;

    if (lit.StartsWith("0x"))
    {
      radix = 16;
      lit = lit[2..];
    }
    else if (lit.StartsWith("0b"))
    {
      radix = 2;
      lit = lit[2..];
    }

    if (lit.Length == 0)
      throw new Exception("Invalid numeric literal");

    BigInteger value = ParseInteger(lit, radix);

    // Your current language semantics treat 0b literals as ByteLiteral.
    if (radix == 2)
    {
      if (suffix != null)
        throw new Exception($"Invalid literal suffix for byte literal: {suffix}. Expected nothing");
      if (unsigned)
        throw new Exception("Byte literal cannot have an unsigned suffix");
      if (value < byte.MinValue || value > byte.MaxValue)
        throw new Exception($"Binary literal {value} does not fit in a byte");
      return new ByteLiteral((byte)value);
    }

    // Explicit short
    if (suffix == 'h')
    {
      if (unsigned)
      {
        if (value < ushort.MinValue || value > ushort.MaxValue)
          throw new Exception($"Literal {value} does not fit in ushort");
        return new UShortLiteral((ushort)value);
      }

      if (value < short.MinValue || value > short.MaxValue)
        throw new Exception($"Literal {value} does not fit in short");
      return new ShortLiteral((short)value);
    }

    // Explicit long
    if (suffix == 'l')
    {
      if (unsigned)
      {
        if (value < ulong.MinValue || value > ulong.MaxValue)
          throw new Exception($"Literal {value} does not fit in ulong");
        return new ULongLiteral((ulong)value);
      }

      if (value < long.MinValue || value > long.MaxValue)
        throw new Exception($"Literal {value} does not fit in long");
      return new LongLiteral((long)value);
    }

    // Unsigned, but width wasn't explicitly forced.
    if (unsigned)
    {
      if (value < 0)
        throw new Exception("Unsigned literal cannot be negative");
      if (value <= uint.MaxValue)
        return new UIntLiteral((uint)value);
      if (value <= ulong.MaxValue)
        return new ULongLiteral((ulong)value);

      throw new Exception($"Integer literal {value} is too large");
    }

    // Normal signed literal: widen automatically.
    if (value >= int.MinValue && value <= int.MaxValue)
      return new IntLiteral((int)value);
    if (value >= long.MinValue && value <= long.MaxValue)
      return new LongLiteral((long)value);
    throw new Exception($"Integer literal {value} is too large");
  }

  private static BigInteger ParseInteger(string text, int radix)
  {
    BigInteger value = 0;

    foreach (char c in text)
    {
      int digit = c switch
        {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => throw new Exception($"Invalid digit '{c}'")
      };

      if (digit >= radix)
        throw new Exception($"Digit '{c}' is invalid in base {radix}");
      value = value * radix + digit;
    }
    return value;
  }
  public DataType GetReturnType();
}

public readonly struct CharLiteral(string Char) : Literal
{
  public string Character {get;} = Char;
  public readonly DataType GetReturnType() => CharType.INSTANCE;
  public override string ToString() => $"'{Character}'";
}

public readonly struct ByteLiteral(byte Byte) : Literal
{
  public byte Byte {get;} = Byte;
  public readonly DataType GetReturnType() => ByteType.INSTANCE;
  public override string ToString() => $"{Byte}";
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
