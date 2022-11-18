using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Waiter = System.Threading.Tasks.TaskCompletionSource;

namespace AsyncSemaphores;

public sealed class FifoSemaphore : ConcurrentSemaphore<ConcurrentQueue<TaskCompletionSource>>
{
  public FifoSemaphore(int initialCount) : base(initialCount, new ConcurrentQueue<TaskCompletionSource>())
  {
  }
}
public sealed class FifoValueTaskSemaphore : ValueTaskConcurrentSemaphore<ConcurrentQueue<ReusableCompletionSource>>
{
  public FifoValueTaskSemaphore(int initialCount) : base(initialCount, new ConcurrentQueue<ReusableCompletionSource>())
  {
  }
}