using System.Collections.Concurrent;
using Waiter = System.Threading.Tasks.TaskCompletionSource;

namespace AsyncSemaphores;

public sealed class LifoSemaphore : ConcurrentSemaphore<ConcurrentStack<TaskCompletionSource>>
{
  public LifoSemaphore(int initialCount) : base(initialCount, new ConcurrentStack<TaskCompletionSource>())
  {
  }
}
public sealed class LifoValueTaskSemaphore : ValueTaskConcurrentSemaphore<ConcurrentStack<ReusableCompletionSource>>
{
  public LifoValueTaskSemaphore(int initialCount) : base(initialCount, new ConcurrentStack<ReusableCompletionSource>())
  {
  }
}