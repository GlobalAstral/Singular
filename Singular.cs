using System.Diagnostics;
using Lexer;
using Parser;
using Tomlyn.Model;
partial class Singular
{
  static readonly TomlTable platforms = ResourceHelper.ExtractToml("platforms");
  static void Main(string[] args)
  {
    try
    {
      main(args);
    } catch (Exception e)
    {
      Logger.Log($"ERROR: {e}");
      Logger.Finalize();
    }
    finally
    {
      Logger.Finalize();
    }
  }
  static void main(string[] args)
  {
    var info = new ProcessStartInfo
    {
      FileName = "gcc",
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    info.ArgumentList.Add("--version");

    using Process gcctest = Process.Start(info)!;

    gcctest.WaitForExit();

    if (gcctest.ExitCode != 0)
      throw new Exception("gcc is not installed");

    // ArgHelper argHelper = new(args);
    ArgHelper argHelper = new([
      "-kC",
      "main.sgl",
    ]);

    argHelper.Parse();

    string gcc = argHelper.GetGcc();
    string output = argHelper.GetOutput();
    string[] importPath = argHelper.GetImportPath();
    string[] inputs = argHelper.GetInputs();
    string cfile = argHelper.GetCFile();
    string host = argHelper.GetHost();

    if (argHelper.GetFlag(ArgHelper.Flag.Debug))
      Logger.EnableDebug();

    List<Token> allContents = [];

    foreach (string input in inputs)
    {
      string content = File.ReadAllText(input);

      Lexer.Lexer lexer = new([.. content], input);
      Token[] tokens = lexer.Process();

      Logger.DebugBlock(tokens, "TOKENS:");

      allContents.AddRange(tokens);
    }

    if (!platforms.TryGetValue(host, out var value) || value is not TomlTable platform)
      throw new PlatformNotSupportedException($"Platform {host} is not currently supported");

    Preprocessor.Preprocessor preprocessor = new([.. allContents], importPath, platform);
    Token[] processed = preprocessor.Flatten(preprocessor.Process());
    
    Logger.DebugBlock(processed, "PREPROCESSED:");

    if (argHelper.GetFlag(ArgHelper.Flag.Preprocessor))
    {
      StreamWriter stream = File.CreateText(output.EndsWith(".exe") ? output.Replace(".exe", ".txt") : $"{output}.txt");
      foreach (Token item in processed)
        stream.WriteLine(item.ToString());
      stream.Close();
      return;
    }

    Parser.Parser parser = new(processed);
    Statement[] statements = parser.Process();
    
    Logger.DebugBlock(statements, "STATEMENTS:");

    Generator.Generator generator = new(statements, parser.GetHashCode());
    string src = string.Join("\n", generator.Process());
    
    Logger.DebugBlock(src, "COMPILED:");

    string clangFormat = ResourceHelper.ExtractClangFormat();
    info = new ProcessStartInfo
    {
      FileName = clangFormat,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    info.ArgumentList.Add("--style=LLVM");
    info.ArgumentList.Add($"--assume-filename={cfile}");

    using Process process = Process.Start(info)!;

    process.StandardInput.Write(src);
    process.StandardInput.Close();

    string formatted = process.StandardOutput.ReadToEnd();
    string errors = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (process.ExitCode != 0)
      throw new Exception(errors);

    StreamWriter f = File.CreateText(cfile);
    f.Write(formatted);
    f.Close();

    string[] gcc_command = gcc.Split(' ');

    info = new ProcessStartInfo
    {
      FileName = gcc_command[0],
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };
    foreach (var arg in gcc_command[1..])
      info.ArgumentList.Add(arg);

    using Process gccproc = Process.Start(info)!;

    string stdout = gccproc.StandardOutput.ReadToEnd();
    string stderr = gccproc.StandardError.ReadToEnd();

    process.WaitForExit();

    Logger.Log(stdout);
    Logger.Log(stderr);

    if (!argHelper.GetFlag(ArgHelper.Flag.KeepC))
      File.Delete(cfile);
  }
}
