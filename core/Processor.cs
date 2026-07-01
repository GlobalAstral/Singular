using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

public abstract class Processor<T, O>(List<T> content) where T: new() where O: new()
{
  protected int peek = 0;
  protected List<O> output = [];

  protected static string EXPECTED_ERROR(T expected) => "Expected " + Convert.ToString(expected);

  [DoesNotReturn]
  protected void Error(string msg) => throw new Exception(msg);

  protected bool HasPeek(int offset = 0) => (peek + offset) >= 0 && (peek + offset) < content.Count;
  protected T Peek(int offset = 0) => HasPeek(offset) ? content[peek + offset] : new T();
  protected bool Peek(T check, int offset = 0)
  {
    return EqualityComparer<T>.Default.Equals(Peek(offset), check);
  }

  protected T Consume() => HasPeek() ? content[peek++] : new T();
  protected void Consume(int amount) { for (int i = 0; i < amount; i++, Consume()); }
  protected bool TryConsume(T consume)
  {
    if (EqualityComparer<T>.Default.Equals(Peek(), consume))
    {
      Consume();
      return true;
    }
    return false;
  }
  protected T TryConsumeError(T consume)
  {
    if (EqualityComparer<T>.Default.Equals(Peek(), consume))
      return Consume();

    Error(EXPECTED_ERROR(consume));
    return new T();
  }

  protected void Alternate(T separator, Action action)
  {
    int amount = 0;
    while (HasPeek())
    {
      if (amount > 0)
        TryConsumeError(separator);
      action();
      amount++;
    }
  }

  protected void DoUntil(T find, Action action)
  {
    bool found = false;
    while (HasPeek())
    {
      if (TryConsume(find))
      {
        found = true;
        break; 
      }
      action();
    }
    if (!found)
      Error(EXPECTED_ERROR(find));
  }
  protected void DoUntil(T find, Action action, T separator)
  {
    int amount = 0;
    DoUntil(find, () => {
      if (amount > 0)
        TryConsumeError(separator);
      action();
      amount++;
    });
  }

  protected void Switch(List<T> i, Action action)
  {
    List<T> prev = content;
    int prev_peek = peek;

    content = i;
    peek = 0;

    action();

    peek = prev_peek;
    content = prev;
  }
  protected void Switch(List<T> i, Action action, T separator) => Switch(i, () => Alternate(separator, action));

  public static Processor<T, O> operator <<(Processor<T, O> processor, O other)
  {
    processor.output.Add(other);
    return processor;
  }

  protected bool CheckAheadFor(T find)
  {
    int old_peek = peek;
    bool found = false;
    while (HasPeek())
    {
      if (TryConsume(find))
      {
        found = true;
        break;
      }
    }
    peek = old_peek;
    return found;
  }

  public abstract List<O> Process();
}