using System.Runtime.CompilerServices;

namespace AsyncSemaphores;

/// <summary>
/// Based on SegmentQueueSynchronizer Semaphore (SIMPLE_ASYNC) from kotlinx.coroutines
/// https://github.com/Kotlin/kotlinx.coroutines/tree/sqs-experiments
/// https://www.youtube.com/watch?v=2uxsNJ0TdIM
/// </summary>
public sealed class SimpleKSemaphore : IValueTaskSemaphoreWithCancellation, ISemaphoreWithCancellation
{
  private static readonly Task<bool> TrueTask = Task.FromResult(true);
  private static readonly Task<bool> FalseTask = Task.FromResult(false);
  
  private long deqIdx;
  private long enqIdx;
  private volatile Segment deqSegm;
  private volatile Segment enqSegm;
  private int availablePermits;

  public SimpleKSemaphore(int permits, int acquiredPermits=0)
  {
    if (permits <= 0)
      throw new ArgumentException($"Semaphore should have at least 1 permit, but had {permits}", nameof(permits));
    if (acquiredPermits < 0 || acquiredPermits > permits)
      throw new ArgumentException($"The number of acquired permits should be in 0..{permits}", nameof(acquiredPermits));
    availablePermits = permits - acquiredPermits;
    deqIdx = enqIdx = 0;
    deqSegm = enqSegm = new Segment(0);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool TryAcquire()
  {
    while (true)
    {
      var p = availablePermits;
      if (p <= 0) return false;
      if (Interlocked.CompareExchange(ref availablePermits, p - 1, p) == p)
        return true;
    }
  }

  public int AvailablePermits => Math.Max(0, availablePermits);
  public int QueueLength => Math.Max(0, -availablePermits);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Task AcquireAsync()
  {
    if (DecrementCounter())
      return Task.CompletedTask; // permit acquired
    var tcs = AcquireSlowPath(CancellationToken.None);
    return tcs?.Task ?? Task.CompletedTask;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValueTask ValueAcquireAsync()
  {
    if (DecrementCounter())
      return ValueTask.CompletedTask; // permit acquired
    var tcs = AcquireValueSlowPath(CancellationToken.None);
    return tcs?.ValueTask ?? ValueTask.CompletedTask;
  }

  private CompletionSource<CancellationData>? AcquireValueSlowPath(CancellationToken token)
  {
    var tcs = CompletionSource<CancellationData>.Get();
    AddAcquireToQueue(tcs, token, out var skipWait);
    return skipWait ? null : tcs;
  }

  private TaskCompletionSource<bool, CancellationData>? AcquireSlowPath(CancellationToken token)
  {
    var tcs = new TaskCompletionSource<bool, CancellationData>();
    AddAcquireToQueue(tcs, token, out var skipWait);
    return skipWait ? null : tcs;
  }

  private void AddAcquireToQueue(ICompeletion<bool, CancellationData> cont, CancellationToken token, out bool skipWait)
  {
    skipWait = false;
    var mySegm = Volatile.Read(ref enqSegm);
    var myEnqIdx = Interlocked.Increment(ref enqIdx) - 1;

    var (id, i) = Math.DivRem(myEnqIdx, Constants.SEGM_SIZE);
    
    var segment = ListEx.FindSegmentAndMoveForward(ref enqSegm, mySegm, id);

    if (token.CanBeCanceled)
      InstallCancellationHandler(cont, token, segment, (byte)i);

    // the regular (fast) path -- if the cell is empty, try to install continuation
    var prev = Interlocked.CompareExchange(ref segment.Items[i], cont, null);
    if (prev == null) // installed continuation successfully
      return;
    if (prev == Constants.PERMIT)
      skipWait = true;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private static void InstallCancellationHandler(ICompeletion<bool, CancellationData> compeletion, CancellationToken token, Segment segment, byte i)
  {
    ref var data = ref compeletion.DataHolder;
    data.Segment = segment;
    data.Index = i;

    data.Registration = token.UnsafeRegister(CancellationCallback, compeletion);
  }

  private static void CancellationCallback(object? o) => CancellationCallback((ICompeletion<bool, CancellationData>)o!);
  private static void CancellationCallback(ICompeletion<bool, CancellationData> cs)
  {
    var segment = cs.DataHolder.Segment;
    var index = cs.DataHolder.Index;

    if (Interlocked.CompareExchange(ref segment.Items[index], Constants.CANCELED, cs) != cs)
      return;

    cs.DataHolder = default!;

    cs.SetResult(false);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release()
  {
      if (IncrementCounter() || ResumeFromQueue())
        return;
      
      ReleaseInternal();
  }

  private void ReleaseInternal()
  {
    while (true)
    {
      if (IncrementCounter() || ResumeFromQueue())
        return;
    }
  }

  public ValueTask<bool> ValueAcquireAsync(CancellationToken token)
  {
    if (token.IsCancellationRequested)
      return ValueTask.FromResult(false);
    if (DecrementCounter())
      return ValueTask.FromResult(true);
    var tcs = AcquireValueSlowPath(token);
    return tcs?.ValueTaskOfT ?? ValueTask.FromResult(true);
  }

  public Task<bool> AcquireAsync(CancellationToken token)
  {
    if (token.IsCancellationRequested)
      return FalseTask;
    if (DecrementCounter())
      return TrueTask;
    var tcs = AcquireSlowPath(token);
    return tcs?.Task ?? TrueTask;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private bool DecrementCounter()
  {
    var p = Interlocked.Decrement(ref availablePermits);
    return p >= 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private bool IncrementCounter()
  {
    var p = Interlocked.Increment(ref availablePermits);
    return p > 0;
  }

  private bool ResumeFromQueue()
  {
    var deqSegm = this.deqSegm;
    var _deqIdx = Interlocked.Increment(ref deqIdx) - 1;
    var (id, i) = Math.DivRem(_deqIdx, Constants.SEGM_SIZE);
    var segment = ListEx.FindSegmentResume(ref this.deqSegm, deqSegm, id);

    if (segment.Id > id)
      return false;
      
    var cellState =
      Interlocked.Exchange(ref segment.Items[i], Constants.PERMIT); // set PERMIT and retrieve the prev cell state
    if (cellState == null)
      return true;
    if (cellState == Constants.CANCELED)
      return false;
    
    var tcs = (ICompeletion<bool, CancellationData>)cellState;
    tcs.DataHolder.Registration.Dispose();
    tcs.SetResult(true);

    return true;
  }

  private struct CancellationData
  {
    public CancellationTokenRegistration Registration;
    public Segment Segment;
    public byte Index;
  }
}
