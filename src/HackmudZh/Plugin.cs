using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace HackmudZh
{
    /// <summary>
    /// hackmud 简体中文模组（BepInEx + Harmony）。
    ///
    /// 设计要点：
    ///   1. 只挂在 Unity/TMP 上，**不引用游戏程序集** —— 不碰混淆名，免疫游戏更新。
    ///   2. 翻译发生在**渲染层**（TMP_Text.set_text 的 prefix）——
    ///      底层逻辑拿到的仍是原始英文，因此不会破坏任何比较/字典键/对象名。
    ///      这正是改字面量方案解决不了的那批"单副本"串能安全翻译的原因。
    ///   3. 中文字形：运行时用系统 CJK 字体造一个动态 TMP 字体资产，
    ///      挂到全局回退 + 已有字体资产的回退表上，不改任何游戏文件。
    ///   4. 词典优先读外部 JSON（可热更新），没有就用 DLL 内嵌的那份。
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class ZhPlugin : BaseUnityPlugin
    {
        public const string Guid = "hackmud.zh.chs";
        public const string Name = "hackmud 简体中文";
        public const string Version = "2.0.7";

        internal static ManualLogSource Log;
        internal static ZhPlugin Instance;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                Translator.Load(this.Info.Location);
                FontFix.Install();
                Patcher.Apply();
                Sweeper.Begin();
                Log.LogInfo(string.Format("hackmud 汉化已加载：词典 {0} 条，字体 {1}",
                    Translator.Count, FontFix.Status));
            }
            catch (Exception e)
            {
                Log.LogError("初始化失败: " + e);
            }
        }
    }

    /// <summary>Harmony 挂点：所有 TMP 文本与 uGUI 文本的设置入口。</summary>
    internal static class Patcher
    {
        internal static void Apply()
        {
            var harmony = new Harmony(ZhPlugin.Guid);
            int ok = 0;

            // 1) TMP：终端与绝大多数界面文本都走这里。
            //    TMP_Text.text 是虚属性，TextMeshProUGUI 未覆盖它 -> 挂基类即可全覆盖。
            var tmpSetter = AccessTools.PropertySetter(typeof(TMP_Text), "text");
            if (tmpSetter != null)
            {
                harmony.Patch(tmpSetter, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(Patches), nameof(Patches.TmpTextPrefix))));
                ok++;
            }
            else ZhPlugin.Log.LogWarning("找不到 TMP_Text.text 的 setter");

            // 2) uGUI Text（按钮、标签等）
            var uiSetter = AccessTools.PropertySetter(typeof(UnityEngine.UI.Text), "text");
            if (uiSetter != null)
            {
                harmony.Patch(uiSetter, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(Patches), nameof(Patches.UiTextPrefix))));
                ok++;
            }
            else ZhPlugin.Log.LogWarning("找不到 UI.Text.text 的 setter");

            ZhPlugin.Log.LogInfo(string.Format("已挂载 {0} 个翻译入口", ok));
        }
    }

    internal static class Patches
    {
        /// <summary>TMP_Text.text = value 的 prefix：把 value 换成译文再交给原方法。</summary>
        internal static void TmpTextPrefix(ref string value)
        {
            value = Translator.Translate(value);
        }

        /// <summary>UI.Text.text = value 的 prefix。</summary>
        internal static void UiTextPrefix(ref string value)
        {
            value = Translator.Translate(value);
        }
    }
}
