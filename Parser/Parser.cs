using System.Runtime.InteropServices;
using Lexer;

namespace Parser;

public partial class Parser(Token[] tokens) : Processor<Token, Statement>(tokens)
{
  private readonly Stack<string> namespaces = [];
  private readonly List<Function> functions = [];
  private readonly Stack<Context> currentContext = [];
  private readonly Stack<DataType?> typeCheckerContext = [];
  private readonly List<Variable> locals = [];
  private readonly Dictionary<string, Struct> structs = [];

  public override Statement ProcessOne()
  {
    Statement? ret =

    Wakeup(Token.Type.NAMESPACE, true, () =>
    {
      string name = (string)TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
      namespaces.Push(name);
      Token[] content = (Token[])TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
      Statement[] statements = Switch(content, Process);
      namespaces.Pop();
      return new Group(statements);
    },

    () => Wakeup(Token.Type.CURLY_BLOCK, false, () =>
    {
      Token[] content = (Token[])Consume().value!;
      
      int saved = locals.Count;
      Statement[] statements = Switch(content, Process);
      locals.RemoveRange(saved, locals.Count - saved);

      return new Scope(statements);
    },
      
    () => Wakeup(Token.Type.FUN, true, ParseFunction,
    
    () => Wakeup(Token.Type.RETURN, true, () =>
    {
      if (currentContext.Count == 0 || currentContext.Peek() is not FunctionContext)
        Error("Cannot return outside of Function Context");

      FunctionContext context = (FunctionContext) currentContext.Peek();

      if (TryConsume(Token.Get(Token.Type.SEMI)))
      {
        if (context.ReturnType != null)
          Error($"Cannot return nothing in a function returning {context.ReturnType}");
        return new Return(null);
      }
      
      Expression expression = ParseExpression(context.ReturnType);

      DataType? temp = expression.GetReturnType();
      if (context.ReturnType != temp)
      {
        string t = context.ReturnType == null ? "nothing" : $"{context.ReturnType}";
        Error($"Cannot return {temp} in a function returning {t}");
      }

      TryConsumeError(Token.Get(Token.Type.SEMI));
      return new Return(expression);
    },

    () => Wakeup(Token.Type.STRUCT, true, () =>
    {
      string ident = MangleIdentifier();
      Token[] body = (Token[]) TryConsumeError(Token.Get(Token.Type.CURLY_BLOCK)).value!;
      List<Statement> group = [];
      Struct s = Switch(body, () =>
      {
        Struct s = new(ident, [], []);
        Context ctx = new StructContext(s);
        structs[ident] = s;
        while (HasPeek())
        {
          if (TryConsume(Token.Get(Token.Type.FUN)))
          {
            currentContext.Push(ctx);
            Statement func = ParseFunction();
            currentContext.Pop();
            group.Add(func);
          }
          else
          {
            ModifierHandler modifiers = GetModifiers(handler =>
            {
              if (handler.IsReadonly) Error("Struct Field cannot be readonly");
              if (!handler.IsStatic)
                handler.Mutable();
            });

            DataType type = ParseType();
            string name = (string) TryConsumeError(Token.Get(Token.Type.IDENTIFIER)).value!;
            
            bool isStatic = modifiers.IsStatic;

            Variable variable = new(modifiers, type, name);
            if (isStatic)
            {
              if (s.Statics.Keys.Any(v => v.Name == variable.Name))
                Error($"Struct static field {variable.Name} already exists");

              Expression? val = null;
              if (TryConsume(Token.Get(Token.Type.EQUALS)))
                val = ParseExpression(variable.Type);
              s.Statics[variable] = val;
            }
            else
            {
              if (s.Fields.Any(v => v.Name == variable.Name))
                Error($"Struct non-static field {variable.Name} already exists");
              s.Fields.Add(variable);
            }
            TryConsumeError(Token.Get(Token.Type.SEMI));
          }
        }
        return s; 
      });
      group.Insert(0, new StructDecl(s));
      return new Group([.. group]);
    },
    
    () => null
    
    )))));
    
    if (ret == null)
      Error("Syntax Error");
    return ret;
  }
}
