using System.Diagnostics;
using Lexer;
using Parser;
partial class Singular
{
  static string ExtractClangFormat()
  {
    string outputDir = Path.Combine(
      "clang-format"
    );

    Directory.CreateDirectory(outputDir);

    string outputPath = Path.Combine(outputDir, "clang-format.exe");

    if (File.Exists(outputPath))
      return outputPath;

    Assembly assembly = Assembly.GetExecutingAssembly();

    string resourceName = "Singular.clang_format.clang-format.exe";

    using Stream? resource = assembly.GetManifestResourceStream(resourceName) ?? throw new Exception($"Could not find embedded resource {resourceName}");
    using FileStream file = File.Create(outputPath);
    resource.CopyTo(file);
    return outputPath;
  }

  static void Main(string[] args)
  {
    // ArgHelper argHelper = new(args);
    ArgHelper argHelper = new([
      "--debug",
      "-kC",
      "main.sgl",
    ]);

    argHelper.Parse();

    string gcc = argHelper.GetGcc();
    string output = argHelper.GetOutput();
    string[] importPath = argHelper.GetImportPath();
    string[] inputs = argHelper.GetInputs();
    string cfile = argHelper.GetCFile();

    List<Token> allContents = [];

    foreach (string input in inputs)
    {
      string content = File.ReadAllText(input);

      Lexer.Lexer lexer = new([.. content], input);
      Token[] tokens = lexer.Process();

      if (argHelper.GetFlag(ArgHelper.Flag.Debug))
      {
        Console.WriteLine("TOKENS:\n");
        foreach (Token item in tokens)
          Console.WriteLine(item.ToString());
      }

      allContents.AddRange(tokens);
    }

    Preprocessor.Preprocessor preprocessor = new([.. allContents], importPath);
    Token[] processed = preprocessor.Process();
    
    if (argHelper.GetFlag(ArgHelper.Flag.Debug))
    {
      Console.WriteLine("PREPROCESSED:\n");
      foreach (Token item in processed)
        Console.WriteLine(item.ToString());
    }

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
    
    if (argHelper.GetFlag(ArgHelper.Flag.Debug))
    {
      Console.WriteLine("Statements:\n");
      foreach (Statement item in statements)
        Console.WriteLine(item);
    }

    Generator.Generator generator = new(statements, parser.GetHashCode());
    string src = string.Join("\n", generator.Process());
    
    if (argHelper.GetFlag(ArgHelper.Flag.Debug))
    {
      Console.WriteLine("Compiling...");
      Console.WriteLine(src);
    }

    string clangFormat = ExtractClangFormat();
    var info = new ProcessStartInfo
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

    Console.Write(stdout);
    Console.Error.Write(stderr);

    if (!argHelper.GetFlag(ArgHelper.Flag.KeepC))
      File.Delete(cfile);
  }
}
