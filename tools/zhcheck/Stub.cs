using BepInEx.Logging;

namespace HackmudZh
{
    /// <summary>测试宿主用的桩：Translator 只用到 ZhPlugin.Log。</summary>
    internal static class ZhPlugin
    {
        internal static ManualLogSource Log = Logger.CreateLogSource("zhcheck");
    }
}
