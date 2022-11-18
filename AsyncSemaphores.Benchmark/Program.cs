// See https://aka.ms/new-console-template for more information

using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;
using AsyncSemaphores;
using AsyncSemaphores.Benchmark;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<SemaphoreBenchmark>();

[MedianColumn]
[ThreadingDiagnoser]
[MemoryDiagnoser]
// [InProcess]
public class SemaphoreBenchmark
{
  [GlobalSetup]
  public void Setup()
  {
    if (!ThreadPool.SetMinThreads(Math.Min(Environment.ProcessorCount, Parallelism),
          Math.Min(Environment.ProcessorCount, Parallelism)))
      throw new Exception();

    if (!ThreadPool.SetMaxThreads(Math.Min(Environment.ProcessorCount, Parallelism),
          Math.Min(Environment.ProcessorCount, Parallelism)))
      throw new Exception();
  }

  [Params(1, 2, 4, 8)]
  public int MaxPermits { get; set; }

  [Params(4, 8, 16)]
  public int Parallelism { get; set; }

  private const int BATCH_SIZE = 128*1024;
  private const int WORK_INSIDE = 240;
  private const int WORK_OUTSIDE = 40;

  // [Benchmark]
  public void Op()
  {
    for (var i = 0; i < 32; ++i)
      doGeomDistrWork(WORK_INSIDE, new Random(i));
  }

  [Benchmark]
  public void NoOp() => BenchmarkSemaphore(new NoOpSemaphore(), MaxPermits);
  
  
  // [Benchmark]
  public void KCSemaphoreTask() => BenchmarkSemaphore((ISemaphore)new KSemaphore(MaxPermits));
  // [Benchmark]
  public void SimpleKSemaphoreTask() => BenchmarkSemaphore((ISemaphore)new SimpleKSemaphore(MaxPermits));
  //
  [Benchmark]
  public void LifoSemaphore() => BenchmarkSemaphore(new LifoSemaphore(MaxPermits));
  //
  [Benchmark]
  public void FifoSemaphore() => BenchmarkSemaphore(new FifoSemaphore(MaxPermits));
  
  [Benchmark]
  public void LifoValueTaskSemaphore() => BenchmarkSemaphore(new LifoValueTaskSemaphore(MaxPermits));
  //
  [Benchmark]
  public void FifoValueTaskSemaphore() => BenchmarkSemaphore(new FifoValueTaskSemaphore(MaxPermits));
  
  // [Benchmark]
  // public void KSemaphoreValue2() => BenchmarkSemaphore(new ValueTaskKSemaphore(MaxPermits));
  [Benchmark]
  public void KSemaphore() => BenchmarkSemaphore(new KSemaphore(MaxPermits));
  [Benchmark]
  public void SimpleKSemaphore() => BenchmarkSemaphore(new SimpleKSemaphore(MaxPermits));
  [Benchmark]
  public void SemaphoreSlim() => BenchmarkSemaphore(new Slim(MaxPermits));

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void BenchmarkSemaphore(ISemaphore semaphore, int? p=null)
  {
    var tasks = new List<Task>();

    var parallelism = p ?? Parallelism;

    var n = BATCH_SIZE / parallelism;
    
    for (int i = 0; i < parallelism; i++)
    {
      var random = new Random(i);
      tasks.Add(Task.Run(async () =>
      {
        var toDo = 0;
        while (toDo < n)
        {
          await semaphore.AcquireAsync();
          try
          {
            toDo++;
            doGeomDistrWork(WORK_INSIDE, random);
          }
          finally
          {
            semaphore.Release();
          }
        }
      }));
    }

    foreach (var task in tasks)
      task.GetAwaiter().GetResult();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void BenchmarkSemaphore(IValueTaskSemaphore semaphore)
  {
    var tasks = new List<Task>();

    var n = BATCH_SIZE / Parallelism;
    
    for (int i = 0; i < Parallelism; i++)
    {
      var random = new Random(i);
      tasks.Add(Task.Run(async () =>
      {
        var toDo = 0;
        while (toDo < n)
        {
          await semaphore.ValueAcquireAsync();
          try
          {
            toDo++;
            doGeomDistrWork(WORK_INSIDE, random);
          }
          finally
          {
            semaphore.Release();
          }
        }
      }));
    }

    foreach (var task in tasks)
      task.GetAwaiter().GetResult();  
  }
  
  [Benchmark]
  public void RateLimiter()
  {
    var tasks = new List<Task>();

    var n = BATCH_SIZE / Parallelism;

    var semaphore = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
    {
      PermitLimit = MaxPermits
    });
    
    for (int i = 0; i < Parallelism; i++)
    {
      var random = new Random(i);
      tasks.Add(Task.Run(async () =>
      {
        var toDo = 0;
        while (toDo < n)
        {
          using (var permit = await semaphore.AcquireAsync())
          {
            if (!permit.IsAcquired)
              continue;
            toDo++;
            doGeomDistrWork(WORK_INSIDE, random);
          }
        }
      }));
    }

    foreach (var task in tasks)
      task.GetAwaiter().GetResult();  
  }

  private static void doGeomDistrWork(int work, Random random) {
    // We use geometric distribution here. We also checked on macbook pro 13" (2017) that the resulting work times
    // are distributed geometrically, see https://github.com/Kotlin/kotlinx.coroutines/pull/1464#discussion_r355705325
    var p = 1.0 / work;
    while (true)
    {
      if (random.NextDouble() < p) break;
    }
  }
}