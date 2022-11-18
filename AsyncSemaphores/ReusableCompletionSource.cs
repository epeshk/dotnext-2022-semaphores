using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace AsyncSemaphores;
public sealed class ReusableCompletionSource : IValueTaskSource
{
  private static readonly ReusableCompletionSource?[] _cache = new ReusableCompletionSource?[64*4]; 
  private ManualResetValueTaskSourceCore<VoidResult> logic;

  public ReusableCompletionSource() => logic.RunContinuationsAsynchronously = true;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void GetResult(short token)
  {
    logic.GetResult(token);
    logic.Reset();

    _cache[(Environment.CurrentManagedThreadId % 64) << 2] ??= this;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValueTaskSourceStatus GetStatus(short token)
  {
    return logic.GetStatus(token);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
  {
    logic.OnCompleted(continuation, state, token, flags);
  }

  public void SetResult() => logic.SetResult(default);

  public static ReusableCompletionSource Get()
  {
    var cache = Interlocked.Exchange(ref _cache[(Environment.CurrentManagedThreadId % 64 ) << 2], null);
    if (cache != null)
      return cache;
    return new ReusableCompletionSource();
  }

  public ValueTask ValueTask => new(this, logic.Version);
}

public interface ICompeletion<TResult>
{
  void SetResult(TResult result);
}
public interface ICompeletion<TResult, TData> : ICompeletion<TResult>
{
  ref TData DataHolder { get; }
}

public sealed class TaskCompletionSource<TResult, TData> : TaskCompletionSource<TResult>, ICompeletion<TResult, TData>
{
  public TaskCompletionSource() : base(TaskCreationOptions.RunContinuationsAsynchronously)
  {
  }

  private TData data;

  public ref TData DataHolder => ref data;
}

public sealed class CompletionSource<TData> : IValueTaskSource<bool>, IValueTaskSource, ICompeletion<bool, TData>
{
  private static readonly CompletionSource<TData>?[] _cache = new CompletionSource<TData>?[64*8]; 
  private ManualResetValueTaskSourceCore<bool> logic;

  private TData data;

  public CompletionSource() => logic.RunContinuationsAsynchronously = true;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool GetResult(short token)
  {
    var result = logic.GetResult(token);
    DataHolder = default!;
    logic.Reset();

    _cache[(Environment.CurrentManagedThreadId % 64) << 2] ??= this;
    return result;
  }

  void IValueTaskSource.GetResult(short token)
  {
    GetResult(token);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValueTaskSourceStatus GetStatus(short token)
  {
    return logic.GetStatus(token);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
  {
    logic.OnCompleted(continuation, state, token, flags);
  }

  public void SetResult(bool result) => logic.SetResult(result);

  public ref TData DataHolder => ref data;

  public static CompletionSource<TData> Get()
  {
    var cache = Interlocked.Exchange(ref _cache[(Environment.CurrentManagedThreadId % 64)<<2], null);
    if (cache != null)
      return cache;
    return new CompletionSource<TData>();
  }

  public ValueTask ValueTask => new(this, logic.Version);
  public ValueTask<bool> ValueTaskOfT => new(this, logic.Version);
}