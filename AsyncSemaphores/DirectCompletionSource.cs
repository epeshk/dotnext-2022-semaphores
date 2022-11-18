using System.Reflection;
using System.Reflection.Emit;

namespace AsyncSemaphores;

public unsafe class DirectCompletionSource
{
  private static Func<object?, TaskCreationOptions, bool, Task> _taskFactory;
  private static delegate*<Task, bool> _trySetResult;

  static DirectCompletionSource()
  {
    _taskFactory = CreateDelegate<Func<object?, TaskCreationOptions, bool, Task>>(
      typeof(Task), new[] { typeof(object), typeof(TaskCreationOptions), typeof(bool) });
    _trySetResult = (delegate*<Task, bool>)typeof(Task)
      .GetMethod("TrySetResult", BindingFlags.NonPublic|BindingFlags.Instance).MethodHandle
      .GetFunctionPointer();

    var task = UnsafeCreateTask();
    UnsafeCompleteTask(task);
    if (!task.IsCompleted)
      throw new Exception();

  }

  public static Task UnsafeCreateTask() => _taskFactory(null, TaskCreationOptions.RunContinuationsAsynchronously, true);
  public static bool UnsafeCompleteTask(Task task) => _trySetResult(task);

  private static T CreateDelegate<T>(Type type, Type[] types) where T : Delegate
  {
    var ctor = type.GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic, types);
    if (ctor == null) throw new MissingMethodException("There is no constructor without defined parameters for this object");
    DynamicMethod dynamic = new DynamicMethod(string.Empty,
      type,
      types,
      type);
    ILGenerator il = dynamic.GetILGenerator();

    il.DeclareLocal(type);
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Ldarg_1);
    il.Emit(OpCodes.Ldarg_2);
    il.Emit(OpCodes.Newobj, ctor);
    il.Emit(OpCodes.Stloc_0);
    il.Emit(OpCodes.Ldloc_0);
    il.Emit(OpCodes.Ret);

    return dynamic.CreateDelegate<T>();
  }
}