using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HackmudZh
{
    /// <summary>
    /// 静态文本扫描器 —— 补上 Harmony 挂点覆盖不到的那一类文本。
    ///
    /// 为什么需要：
    ///   挂 `TMP_Text.set_text` 只能拦到**运行时赋值**的文本。
    ///   而写在预制体/场景里的**静态 UI 文本**（面板标题、按钮标签、占位符等）
    ///   在序列化时就带上了内容，**根本不会调用 set_text** —— 于是永远译不到。
    ///
    /// 做法：
    ///   周期性遍历 `Resources.FindObjectsOfTypeAll<TMP_Text>()` 与 `UnityEngine.UI.Text`，
    ///   把每个组件的 `.text` 过一遍翻译器；有变化才写回。
    ///   写回会再次触发我们的 set_text prefix —— 但中文不会再命中词典，所以是幂等的。
    ///
    /// 频率：前 20 秒每秒一次（覆盖启动与首个场景），之后每 5 秒一次（覆盖切场景/新面板）。
    /// </summary>
    internal class Sweeper : MonoBehaviour
    {
        private static Sweeper _inst;
        private int _ticks;
        private int _changedTotal;
        private int _scannedTotal;

        /// <summary>诊断用：记录「翻译后仍含英文」的渲染文本。
        /// 这是「该翻译的地方还是没翻译」的**权威来源** —— 直接读渲染层，
        /// 不依赖 OCR、不依赖 shell.txt（后者存的是未折行的原文）。</summary>
        private static readonly HashSet<string> _logged = new HashSet<string>();
        private static int _loggedCount;

        private static readonly System.Text.RegularExpressions.Regex EnWord =
            new System.Text.RegularExpressions.Regex("[A-Za-z]{3,}",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>由插件在 Awake 里调用（不能叫 Start：会与 Unity 的 Start 消息重名）</summary>
        internal static void Begin()
        {
            if (_inst != null) return;
            var go = new GameObject("HackmudZh.Sweeper");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _inst = go.AddComponent<Sweeper>();
            ZhPlugin.Log.LogInfo("静态文本扫描器已启动");
        }

        private void Start()
        {
            StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                float wait = _ticks < 20 ? 1f : 5f;
                yield return new WaitForSecondsRealtime(wait);
                _ticks++;
                try { Sweep(); }
                catch (System.Exception e) { ZhPlugin.Log.LogWarning("扫描异常: " + e.Message); }
            }
        }

        private void Sweep()
        {
            int changed = 0, scanned = 0;

            // 1) TextMeshPro（终端与绝大多数界面）
            TMP_Text[] tmps = null;
            try { tmps = Resources.FindObjectsOfTypeAll<TMP_Text>(); }
            catch (System.Exception e) { ZhPlugin.Log.LogWarning("枚举 TMP_Text 失败: " + e.Message); }
            if (tmps != null)
            {
                foreach (var t in tmps)
                {
                    if (t == null) continue;
                    scanned++;
                    try
                    {
                        var cur = t.text;
                        if (string.IsNullOrEmpty(cur)) continue;
                        var tr = Translator.Translate(cur);
                        if (!ReferenceEquals(tr, cur) && tr != cur)
                        {
                            t.text = tr;      // 经我们的 prefix，幂等
                            changed++;
                            cur = tr;
                        }
                        // 诊断：翻译后仍含英文 = 词典缺这条（记录**渲染层的真实文本**）
                        NoteUntranslated(cur);
                    }
                    catch { }
                }
            }

            // 2) uGUI Text
            UnityEngine.UI.Text[] uis = null;
            try { uis = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>(); }
            catch { }
            if (uis != null)
            {
                foreach (var t in uis)
                {
                    if (t == null) continue;
                    scanned++;
                    try
                    {
                        var cur = t.text;
                        if (string.IsNullOrEmpty(cur)) continue;
                        var tr = Translator.Translate(cur);
                        if (!ReferenceEquals(tr, cur) && tr != cur)
                        {
                            t.text = tr;
                            changed++;
                        }
                    }
                    catch { }
                }
            }

            _scannedTotal = scanned;
            if (changed > 0)
            {
                _changedTotal += changed;
                ZhPlugin.Log.LogInfo(string.Format("静态文本扫描: 本轮译了 {0} 个组件（累计 {1}，本轮扫描 {2} 个）",
                    changed, _changedTotal, scanned));
            }
        }

        /// <summary>判断文本里是否还有「连续 3 个 ASCII 字母」，**跳过所有 &lt;...&gt; 区段**。
        ///
        /// 为什么不用正则：最初用 `</?color(?:=#[0-9A-Fa-f]{6,8})?>` 剥标签，
        /// 单独在 .NET 里测完全正确，但编进插件后**行为不符** ——
        /// `&lt;color=#00FFFFFF&gt;已加入公司：否&lt;/color&gt;` 仍被当成含英文而记进未译清单
        /// （色值 `00FFFFFF` 被 `[A-Za-z]{3,}` 匹配）。逐字符扫描没有这个问题，
        /// 行为完全确定，也顺带省掉一个正则。
        ///
        /// 注意 `&lt;mark_name&gt;` 这类占位符也会被跳过 —— 那是**想要**的：
        /// 它是玩家要输入的内容，不该算作「未译的英文」。</summary>
        private static bool HasEnglishWord(string s)
        {
            bool inTag = false;
            int run = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<') { inTag = true; run = 0; continue; }
                if (c == '>') { inTag = false; run = 0; continue; }
                if (inTag) continue;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    if (++run >= 3) return true;
                }
                else run = 0;
            }
            return false;
        }

        /// <summary>把一个「译后仍含英文」的短文本登记进诊断清单（去重、限量）。</summary>
        private static void NoteUntranslated(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (s.Length > 300) s = s.Substring(0, 300);
            if (!HasEnglishWord(s)) return;
            if (!_logged.Add(s)) return;
            _loggedCount++;
            if (_loggedCount <= 300)
                ZhPlugin.Log.LogWarning("[未译] " + s.Replace("\n", "\\n"));
        }
    }
}
