namespace AsyncSemaphores;

internal static class Constants
{
  public static object PERMIT = nameof(PERMIT);
  public static object CANCELED = nameof(CANCELED);
  public const int SEGM_SIZE = 256;
  public const int SEGM_SIZE_CANCELLABLE = 16;
}