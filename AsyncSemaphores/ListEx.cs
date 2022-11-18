using System.Runtime.CompilerServices;

namespace AsyncSemaphores;

internal static class ListEx
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static CSegment FindSegmentAndMoveForward(ref CSegment from, CSegment start, long id)
  {
    while (true)
    {
      var to = FindSegment(start, id);
      if (MoveForward(ref from, to))
        return to;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Segment FindSegmentAndMoveForward(ref Segment from, Segment start, long id)
  {
    var to = FindSegment(start, id);

    var cur = from;
    while (cur.Id < to.Id)
      cur = Interlocked.CompareExchange(ref from, to, cur);
    return to;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Segment FindSegmentResume(ref Segment originalRef, Segment start, long id)
  {
    Segment segment = FindSegment(start, id);
    while (true)
    {
      var cur = originalRef;
      if (cur.Id >= segment.Id)
        return segment;
      if (Interlocked.CompareExchange(ref originalRef, segment, cur) == cur)
        return segment;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool MoveForward(ref CSegment from, CSegment to)
  {
    while (true)
    {
      var cur = from;
      if (cur.Id >= to.Id) return true;
      if (!to.IncrementPointers()) return false;
      if (Interlocked.CompareExchange(ref from, to, cur) == cur)
      {
        if (cur.DecrementPointers())
          cur.Remove();
        return true;
      }
      if (to.DecrementPointers())
        to.Remove();
    }
  }
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void MoveForward(ref Segment from, Segment to)
  {
    var cur = from;
    while (cur.Id < to.Id)
      cur = Interlocked.CompareExchange(ref from, to, cur);
  }
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static Segment FindSegment(Segment start, long id)
  {
    var cur = start;
    while (cur.Id < id)
    {
      var next = cur.Next;
      if (next != null)
      {
        cur = next;
        continue;
      }
      
      var newTail = new Segment(cur.Id + 1);
      cur = Interlocked.CompareExchange(ref cur.Next, newTail, null) ?? newTail;
    }

    return cur;
  }

  private static CSegment FindSegment(CSegment start, long id)
  {
    var cur = start;
    while (cur.Id < id || cur.Removed)
    {
      var next = cur.Next;
      if (next != null)
      {
        cur = next;
        continue;
      }
      
      var newTail = new CSegment(cur.Id + 1, cur, 1);
      var installedTail = Interlocked.CompareExchange(ref cur.Next, newTail, null) ?? newTail;
      if (installedTail == newTail && cur.DecrementPointers())
        cur.Remove();

      cur = installedTail;
    }

    return cur;
  }
}
