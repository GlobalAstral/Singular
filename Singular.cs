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
    List<Token> tokens = lexer.Process();
    Console.WriteLine("TOKENS:\n");
    tokens.ForEach(token => Console.WriteLine(token));

    Parser.Parser parser = new(tokens);
    List<SyntaxInstance> statements = parser.Process();
    Console.WriteLine("Statements:\n");
    statements.ForEach(statement => Console.WriteLine(statement));

  }
}
