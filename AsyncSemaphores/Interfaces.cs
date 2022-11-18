namespace AsyncSemaphores;

public interface ISemaphore
{
  Task AcquireAsync();
  bool TryAcquire();
  void Release();
}
public interface IValueTaskSemaphore
{
  ValueTask ValueAcquireAsync();
  bool TryAcquire();
  void Release();
}
public interface IValueTaskSemaphoreWithCancellation : IValueTaskSemaphore
{
  ValueTask<bool> ValueAcquireAsync(CancellationToken token);
}

public interface ISemaphoreWithCancellation : ISemaphore
{
  Task<bool> AcquireAsync(CancellationToken token);
}