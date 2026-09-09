using System.IO.Compression;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

public class ArgHelper(string[] Args)
{
  public enum Flag
  {
    Debug,
    Preprocessor,
    KeepC,
  }

  private int flags = 0;
  private string? gcc = null;
  private readonly List<string> inputs = [];
  private string? output = null;
  private readonly List<string> importpath = [];
  private string? cfile = null;

  private static int Btoi(bool b) => b ? 1 : 0;
  private void SetFlag(Flag flag, bool value) => flags |= Btoi(value) << (int) flag;
  public bool GetFlag(Flag flag) => ((flags >> (int) flag) & (byte) 1) == 1;
  public string GetGcc() => gcc!;
  public string[] GetInputs() => [ .. inputs ];
  public string GetOutput() => output!;
  public string[] GetImportPath() => [ .. importpath ];
  public string GetCFile() => cfile!;

  public void Parse()
  {
    while (HasPeek())
    {
      if (TryConsume("--debug"))
        SetFlag(Flag.Debug, true);
      else if (TryConsume("-E"))
        SetFlag(Flag.Preprocessor, true);
      else if (TryConsume("-kC"))
        SetFlag(Flag.KeepC, true);
      
      else if (TryConsume("-gcc"))
        gcc = Consume();
      else if (TryConsume("-o"))
        output = Consume();
      else if (TryConsume("-I"))
        importpath.Add(Consume());
      else
        inputs.Add(Consume());
    }

    if (inputs.Count == 0)
      throw new Exception("No input files in command line arguments");
    if (output == null && inputs.Count > 1)
      throw new Exception("Cannot omit output file with more than a single input");

    if (!inputs.All(i => i.EndsWith(".sgl")))
      throw new Exception("Not all input files are Singular .sgl files");

    output ??= inputs[0].Replace(".sgl", OperatingSystem.IsWindows() ? ".exe" : "");

    cfile = output.EndsWith(".exe") ? output.Replace(".exe", ".c") : $"{output}.c";
    gcc ??= $"gcc -Wall -Wextra {cfile} -o {output}";
    gcc = gcc.Replace("$out", cfile);
  }

  private int peek = 0;

  private bool HasPeek(int offset = 0) => peek + offset < Args.Length;
  private string Peek(int offset = 0) => HasPeek(offset) ? Args[peek + offset] : "";
  private bool PeekEqual(string s, int offset = 0) => Peek(offset) == s;
  private string Consume(int amount = 1, string sep = " ") => HasPeek(amount - 1) ? string.Join(sep, Args[peek..(peek += amount)]) : "";
  private bool TryConsume(string s)
  {
    if (PeekEqual(s))
    {
      Consume();
      return true;
    }
    return false;
  }
}
