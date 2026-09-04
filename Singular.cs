using System.Diagnostics;
using System.Reflection;
using Lexer;
using Parser;
partial class Singular
{
  public static readonly string SRC_EXT = ".sgl";

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
    // if (args.Length < 1)
    //   throw new ArgumentException("Invalid command line arguments");

    // string name = args[0];
    // if (!name.EndsWith(SRC_EXT))
    //   throw new ArgumentException("Invalid file extension. Expected " + SRC_EXT, name);

    string name = "test.sgl";
    
    string content = File.ReadAllText(name);

    Lexer.Lexer lexer = new([.. content], name);
    Token[] tokens = lexer.Process();
    Console.WriteLine("TOKENS:\n");
    foreach (Token item in tokens)
      Console.WriteLine(item);

    Parser.Parser parser = new(tokens);
    Statement[] statements = parser.Process();
    Console.WriteLine("Statements:\n");
    foreach (Statement item in statements)
      Console.WriteLine(item);

    Generator.Generator generator = new(statements, parser.GetHashCode());
    Console.WriteLine("Compiling...");
    string output = string.Join("\n", generator.Process());
    Console.WriteLine(output);

    string outputfile = "test.c";

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
    info.ArgumentList.Add($"--assume-filename={outputfile}");

    using Process process = Process.Start(info)!;

    process.StandardInput.Write(output);
    process.StandardInput.Close();

    string formatted = process.StandardOutput.ReadToEnd();
    string errors = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (process.ExitCode != 0)
      throw new Exception(errors);

    StreamWriter f = File.CreateText(outputfile);
    f.Write(formatted);
    f.Close();
  }
}
