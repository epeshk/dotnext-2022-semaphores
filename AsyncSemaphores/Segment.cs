using System.Buffers;
using System.Runtime.CompilerServices;

namespace AsyncSemaphores;

public sealed class Segment
{
  public long Id { get; }
  public volatile Segment? Next;
  public readonly object?[] Items = new object[Constants.SEGM_SIZE];

  public Segment(long id) => Id = id;
}

public sealed class CSegment
{
  private int removeCounter = 0;
  public long Id { get; }
  public volatile CSegment? Next;
  public volatile CSegment? Prev;
  public object?[] Items = new object?[Constants.SEGM_SIZE_CANCELLABLE];
  private volatile int Count;


  public CSegment(long id, CSegment? prev, int pointers)
  {
    Id = id;
    Prev = prev;
    Count = pointers << 16;
  }

  public void IncrementCanceled()
  {
    if (Interlocked.Increment(ref Count) == Constants.SEGM_SIZE_CANCELLABLE)
      Remove();
  }

  public bool IncrementPointers()
  {
    while (true)
    {
      var count = Count;
      if ((count & ((1 << 16) - 1)) == Constants.SEGM_SIZE_CANCELLABLE)
        return false;
      if (Interlocked.CompareExchange(ref Count, count + (1 << 16), count) == count)
        return true;
    }
  }

  private bool IsTail => Volatile.Read(ref Next) == null;

  public bool DecrementPointers() => Interlocked.Add(ref Count, -(1 << 16)) == Constants.SEGM_SIZE_CANCELLABLE;
  
  public bool Removed
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => Count == Constants.SEGM_SIZE_CANCELLABLE;
  }

  public void Remove()
  {
    if (Interlocked.Increment(ref removeCounter) > 1) throw new Exception(Count.ToString());
    while (true)
    {
      if (IsTail) return;
      var prev = FindLeftAliveSegment();
      var next = FindRightAliveSegment();
  
      next.Prev = prev;
      if (prev != null)
        prev.Next = next;
      if (next.Removed && !next.IsTail)
        continue;
      if (prev?.Removed ?? false)
        continue;
      
      // Is it safe to recycle items here?
      // Items.AsSpan().Fill(null);
      // lockFreePool.Return(Items);
      // Items = null!;
      return;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public CSegment? FindLeftAliveSegment()
  {
    var cur = Prev;
    while (cur is { Removed: true })
      cur = cur.Prev;
    return cur;
  }
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public CSegment FindRightAliveSegment()
  {
    var cur = Next!;
    while (cur.Removed && !cur.IsTail)
      cur = cur.Next;
    return cur;
  }
}