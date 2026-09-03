using System.Text;

namespace Generator;

public class Context
{
  private readonly StringBuilder CodePrologue = new();
  public void Prologue(string add) => CodePrologue.Append(add);
  public string Prologue() => CodePrologue.ToString();
}
