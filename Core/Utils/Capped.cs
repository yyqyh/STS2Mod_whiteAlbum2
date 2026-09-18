namespace STS_WhiteAlbum2.Core.Ancients;

/// <summary>同一条消息只打一次的日志工具（补丁会被反复调用，避免刷屏）。</summary>
internal static class Capped
{
    private static readonly HashSet<string> Seen = [];

    public static void LogOnce(string message)
    {
        lock (Seen)
        {
            if (!Seen.Add(message))
            {
                return;
            }
        }

        MegaCrit.Sts2.Core.Logging.Log.Info(message);
    }
}
