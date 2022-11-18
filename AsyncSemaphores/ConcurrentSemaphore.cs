using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Waiter = System.Threading.Tasks.TaskCompletionSource;

namespace AsyncSemaphores;

public abstract class ConcurrentSemaphore<T> : ISemaphore where T : IProducerConsumerCollection<Waiter>
{
  private readonly T waiters;
  private readonly WaitCallback signalDeferredWaiterCallback;
  private readonly WaitCallback signalDeferredWaitersCallback;
  private volatile int currentCount;

  protected ConcurrentSemaphore(int initialCount, T collection)
  {
    if (initialCount < 0)
      throw new ArgumentOutOfRangeException(nameof(initialCount), $"Initial count must not be negative, but was '{initialCount}'.");

    currentCount = initialCount;

    waiters = collection;

    signalDeferredWaiterCallback = _ => SignalDeferredWaiter();
    signalDeferredWaitersCallback = count => SignalDeferredWaiters((int)count);
  }

  public bool TryAcquire()
  {
    while (true)
    {
      var current = currentCount;
      if (current <= 0) return false;
      if (Interlocked.CompareExchange(ref currentCount, current - 1, current) == current)
        return true;
    }
  }

  public int CurrentCount => Math.Max(0, currentCount);

  public int CurrentQueue => Math.Max(0, -currentCount);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Task AcquireAsync()
  {
    var decrementedCount = Interlocked.Decrement(ref currentCount);
    if (decrementedCount >= 0)
      return Task.CompletedTask;

    var waiter = new Waiter(TaskCreationOptions.RunContinuationsAsynchronously);

    waiters.TryAdd(waiter);

    return waiter.Task;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release()
  {
    var countBeforeRelease = Interlocked.Increment(ref currentCount) - 1;
    if (countBeforeRelease < 0)
    {
      SignalWaiter();
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release(int count)
  {
    if (count < 0)
      throw new ArgumentOutOfRangeException(nameof(count), $"Release count must not be negative, but got value '{count}'.");

    if (count == 0)
      return;

    var countBeforeRelease = Interlocked.Add(ref currentCount, count) - count;
    if (countBeforeRelease < 0)
    {
      SignalWaiters(Math.Min(count, -countBeforeRelease));
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalWaiter()
  {
    if (waiters.TryTake(out var waiter))
    {
      waiter.SetResult();
    }
    else
    {
      ThreadPool.UnsafeQueueUserWorkItem(signalDeferredWaiterCallback, null);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalWaiters(int count)
  {
    var remainingCount = count;

    for (var i = 0; i < count; i++)
    {
      if (!waiters.TryTake(out var waiter))
        break;

      waiter.SetResult();

      remainingCount--;
    }

    if (remainingCount > 0)
    {
      ThreadPool.UnsafeQueueUserWorkItem(signalDeferredWaitersCallback, remainingCount);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalDeferredWaiter()
  {
    Waiter waiter;

    var spinner = new SpinWait();

    while (!waiters.TryTake(out waiter))
    {
      spinner.SpinOnce();
    }

    waiter.SetResult();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalDeferredWaiters(int count)
  {
    for (var i = 0; i < count; i++)
    {
      SignalDeferredWaiter();
    }
  }
}

public abstract class ValueTaskConcurrentSemaphore<T> : IValueTaskSemaphore where T : IProducerConsumerCollection<ReusableCompletionSource>
{
  private readonly T waiters;
  private readonly WaitCallback signalDeferredWaiterCallback;
  private readonly WaitCallback signalDeferredWaitersCallback;
  private volatile int currentCount;

  protected ValueTaskConcurrentSemaphore(int initialCount, T collection)
  {
    if (initialCount < 0)
      throw new ArgumentOutOfRangeException(nameof(initialCount), $"Initial count must not be negative, but was '{initialCount}'.");

    currentCount = initialCount;

    waiters = collection;

    signalDeferredWaiterCallback = _ => SignalDeferredWaiter();
    signalDeferredWaitersCallback = count => SignalDeferredWaiters((int)count);
  }

  public bool TryAcquire()
  {
    while (true)
    {
      var current = currentCount;
      if (current < 1) return false;
      if (Interlocked.CompareExchange(ref currentCount, current - 1, current) == current)
        return true;
    }
  }

  public int CurrentCount => Math.Max(0, currentCount);

  public int CurrentQueue => Math.Max(0, -currentCount);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValueTask ValueAcquireAsync()
  {
    var decrementedCount = Interlocked.Decrement(ref currentCount);
    if (decrementedCount >= 0)
      return ValueTask.CompletedTask;

    var waiter = ReusableCompletionSource.Get();

    waiters.TryAdd(waiter);

    return waiter.ValueTask;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release()
  {
    var countBeforeRelease = Interlocked.Increment(ref currentCount) - 1;
    if (countBeforeRelease < 0)
    {
      SignalWaiter();
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release(int count)
  {
    if (count < 0)
      throw new ArgumentOutOfRangeException(nameof(count), $"Release count must not be negative, but got value '{count}'.");

    if (count == 0)
      return;

    var countBeforeRelease = Interlocked.Add(ref currentCount, count) - count;
    if (countBeforeRelease < 0)
    {
      SignalWaiters(Math.Min(count, -countBeforeRelease));
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalWaiter()
  {
    if (waiters.TryTake(out var waiter))
    {
      waiter.SetResult();
    }
    else
    {
      ThreadPool.UnsafeQueueUserWorkItem(signalDeferredWaiterCallback, null);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalWaiters(int count)
  {
    var remainingCount = count;

    for (var i = 0; i < count; i++)
    {
      if (!waiters.TryTake(out var waiter))
        break;

      waiter.SetResult();

      remainingCount--;
    }

    if (remainingCount > 0)
    {
      ThreadPool.UnsafeQueueUserWorkItem(signalDeferredWaitersCallback, remainingCount);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalDeferredWaiter()
  {
    ReusableCompletionSource waiter;

    var spinner = new SpinWait();

    while (!waiters.TryTake(out waiter))
    {
      spinner.SpinOnce();
    }

    waiter.SetResult();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SignalDeferredWaiters(int count)
  {
    for (var i = 0; i < count; i++)
    {
      SignalDeferredWaiter();
    }
  }
}