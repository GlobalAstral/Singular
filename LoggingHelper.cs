
using System.Text;

public static class Logger
{
  public static void EnableDebug() => NeedsDebug = true;
  private static bool NeedsDebug = false;
  private static readonly StringBuilder Output = new();
  private static readonly StringBuilder Debugs = new();

  public static void Log(string msg) => Output.Append($"{msg}\n");
  public static void Log<T>(IEnumerable<T> values) { foreach (T t in values) Log($"{t}"); }
  public static void Debug(string msg) => Debugs.Append($"{msg}\n");
  public static void Debug<T>(IEnumerable<T> values) { foreach (T t in values) Debug($"{t}"); }
  public static void Separator() => Output.Append("\n\n\n");
  public static void DbgSeparator() => Debugs.Append("\n\n\n");
  public static void DebugBlock(string msg)
  {
    DbgSeparator();
    Debug(msg);
    DbgSeparator();
  }
  public static void DebugBlock<T>(IEnumerable<T> values)
  {
    DbgSeparator();
    foreach (T t in values)
      Debug($"{t}");
    DbgSeparator();
  }
  public static void DebugBlock<T>(IEnumerable<T> values, string title)
  {
    Debug(title);
    DebugBlock(values);
  }
  public static void DebugBlock(string msg, string title)
  {
    Debug(title);
    DebugBlock(msg);
  }

  public static void Block(string msg)
  {
    Separator();
    Log(msg);
    Separator();
  }

  public static void Block(string msg, string title)
  {
    Log(title);
    Block(msg);
  }
  public static void Block<T>(IEnumerable<T> values)
  {
    Separator();
    foreach (T t in values)
      Log($"{t}");
    Separator();
  }
  public static void Block<T>(IEnumerable<T> values, string title)
  {
    Log(title);
    Block(values);
  }
  public static void Finalize()
  {
    System.Diagnostics.Debug.WriteLineIf(NeedsDebug, Debugs.ToString());
    Console.WriteLine(Output.ToString());
  }
}
