using Lexer;
using Parser;
partial class Singular
{
  public static readonly string SRC_EXT = ".sgl";

  static void Main(string[] args)
  {
    // if (args.Length < 1)
    //   throw new ArgumentException("Invalid command line arguments");

    // string name = args[0];
    // if (!name.EndsWith(SRC_EXT))
    //   throw new ArgumentException("Invalid file extension. Expected " + SRC_EXT, name);

    string name = "test.sgl";
    
    string content = File.ReadAllText(name);

    Lexer.Lexer lexer = new([.. content]);
    Token[] tokens = lexer.Process();
    Console.WriteLine("TOKENS:\n");
    foreach (Token item in tokens)
      Console.WriteLine(item);

    Parser.Parser parser = new(tokens);
    Statement[] statements = parser.Process();
    Console.WriteLine("Statements:\n");
    foreach (Statement item in statements)
      Console.WriteLine(item);
  }
}
