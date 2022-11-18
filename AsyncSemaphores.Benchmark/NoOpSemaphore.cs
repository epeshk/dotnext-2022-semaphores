using System.Runtime.CompilerServices;

namespace AsyncSemaphores.Benchmark;

public class NoOpSemaphore : ISemaphore
{
  public Task AcquireAsync()
  {
    return Task.CompletedTask;
    // await TaskScheduler.Default;
  }

  public bool TryAcquire()
  {
    return true;
  }

  public void Release()
  {
  }
}

internal static class Extensions
{
  public static TaskSchedulerAwaiter GetAwaiter(this TaskScheduler scheduler) => new(scheduler);
}

internal readonly struct TaskSchedulerAwaiter : ICriticalNotifyCompletion
{
  private static readonly Action<Action> callback = action => action();

  private readonly TaskScheduler _scheduler;
  public TaskSchedulerAwaiter(TaskScheduler scheduler) => _scheduler = scheduler;
  public bool IsCompleted => false;
  public void GetResult() { }
  public void OnCompleted(Action continuation) => Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.DenyChildAttach, _scheduler);
  public void UnsafeOnCompleted(Action continuation)
  {
    if (ReferenceEquals(_scheduler, TaskScheduler.Default))
    {
      ThreadPool.UnsafeQueueUserWorkItem(callback, continuation, preferLocal: true);
    }
    else
    {
      OnCompleted(continuation);
    }
  }
}

public class Slim : ISemaphore
{
  private readonly SemaphoreSlim semaphoreSlim;

  public Slim(int permits) => semaphoreSlim = new SemaphoreSlim(permits);

  public Task AcquireAsync()
    => semaphoreSlim.WaitAsync();

  public bool TryAcquire()
    => semaphoreSlim.Wait(0);

  public void Release()
    => semaphoreSlim.Release();
}