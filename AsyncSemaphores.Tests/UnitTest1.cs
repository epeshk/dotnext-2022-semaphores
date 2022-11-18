namespace AsyncSemaphores.Tests;

public class Tests
{
  [SetUp]
  public void Setup()
  {
  }

  [Test, Timeout(25000)]
  public void Test1()
  {
    TestCancellableSemaphore(new KSemaphore(1), 1, 8, 32, TimeSpan.FromSeconds(20));
  }

  private void TestCancellableSemaphore(
    IValueTaskSemaphoreWithCancellation semaphore,
    int permitLimit,
    int threadsWithoutCancellation,
    int threadsWithCancellation,
    TimeSpan timeout)
  {
    var tasks = new List<Task<long>>();
    var parallelism = 0;
    var cancelled = 0;
    
    using var timeoutCts = new CancellationTokenSource(timeout);
    var token = timeoutCts.Token;
    
    for (int i = 0; i < threadsWithoutCancellation; i++)
    {
      tasks.Add(Task.Run(async () =>
      {
        long passed = 0;
       
        while (!token.IsCancellationRequested)
        {
          await semaphore.ValueAcquireAsync();
          var cur = Interlocked.Increment(ref parallelism);
          if (cur > permitLimit)
            throw new Exception($"parallelism: {cur} > {permitLimit}");

          passed++;
          Interlocked.Decrement(ref parallelism);
          semaphore.Release();
        }

        return passed;
      }));
    }

    for (int i = 0; i < threadsWithCancellation; i++)
    {

      var random = new Random(i);
      tasks.Add(Task.Run(async () =>
      {
        long passed = 0;
        
        while (!token.IsCancellationRequested)
        {
          using var cts = new CancellationTokenSource();
          var vtask = semaphore.ValueAcquireAsync(cts.Token);
          var nextDouble = random.NextDouble();
          if (nextDouble < 0.5)
          {
            await Task.Delay(random.Next(0, 2));
            cts.Cancel();
          }
          var result = await vtask;
          if (!result)
          {
            Interlocked.Increment(ref cancelled);
            continue;
          }
    
          var cur = Interlocked.Increment(ref parallelism);
          if (cur > permitLimit)
            throw new Exception($"parallelism: {cur} > {permitLimit}");
          passed++;
          Interlocked.Decrement(ref parallelism);
          semaphore.Release();
        }
    
        return passed;
      }));
    }

    
    Thread.Sleep(2000);
    var passed = tasks.Select(x => x.GetAwaiter().GetResult()).Sum();


    for (int i = 0; i < parallelism; i++)
      semaphore.ValueAcquireAsync().AsTask().Wait(TimeSpan.Zero);
    
    Console.WriteLine($"Cancelled: {cancelled}, Passed: {passed}");
  }
}