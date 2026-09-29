using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HackmudZh
{
    /// <summary>
    /// 运行时中文字形注入 —— 不修改任何游戏文件。
    ///
    /// 游戏自带 TMP 字体不含 CJK 字形。这里造一个**动态** TMP 字体资产并挂成回退字体
    /// （全局 TMP_Settings.fallbackFontAssets + 每个已加载字体资产的 fallbackFontAssetTable），
    /// TMP 找不到字形时自动回退，无需改动任何组件或资源文件。
    ///
    /// 建字体资产有三条策略（依次尝试，每条都记录失败原因，便于诊断）：
    ///   A. CreateFontAsset(string familyName, string styleName, int pointSize)  —— 按系统字体族名
    ///   B. CreateFontAsset(string fontFilePath, int faceIndex, ...)             —— 按字体文件路径
    ///   C. CreateFontAsset(Font, ...)                                           —— 按 Font 对象
    /// </summary>
    internal static class FontFix
    {
        internal static string Status = "未安装";
        private static TMP_FontAsset _cjk;

        private static readonly string[] Families =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
            "SimHei", "黑体", "Noto Sans CJK SC", "Source Han Sans SC",
            "SimSun", "宋体", "Microsoft JhengHei", "Arial Unicode MS"
        };
        private static readonly string[] Styles = { "Regular", "Normal", "regular" };

        private static readonly string[] FontFiles =
        {
            @"C:\Windows\Fonts\msyh.ttc", @"C:\Windows\Fonts\msyh.ttf",
            @"C:\Windows\Fonts\msyhbd.ttc", @"C:\Windows\Fonts\simhei.ttf",
            @"C:\Windows\Fonts\simsun.ttc", @"C:\Windows\Fonts\Deng.ttf",
            @"C:\Windows\Fonts\msjh.ttc"
        };

        private static readonly string Prewarm =
            "的一是在不了有和人这中大为上个国我以要他时来用们生到作地于出就分对成会可主发年动" +
            "同工也能下过子说产种面而方后多定行学法所民得经十三之进着等部度家电力里如水化高自" +
            "二理起小物现实加量都两体制机当使点从业本去把性好应开它合还因由其些然前外天政四日" +
            "那社义事平形相全表间样与关各重新线内数正心反你明看原又么利比或但质气第向道命此变" +
            "条只没结解问意建月公无系军很情者最立代想已通并提直题党程展五果料象员革位入常文总" +
            "次品式活设及管特件长求老头基资边流路级少图山统接知较将组见计别她手角期根论运农指" +
            "几九区强放决西被干做必战先回则任取据处队南给色光门即保治北造百规热领七海口东导器" +
            "压志世金增争济阶油思术极交受联什认六共权收证改清己美再采转更单风切打白教速花带安" +
            "场身车例真务具万每目至达走积示议声报斗完类八离华名确才科张信马节话米整空元况今集" +
            "温传土许步群广石记需段研界拉林律叫且究观越织装影算低持音众书布复容儿须际商非验连" +
            "断深难近矿千周委素技备半办青省列习响约支般史感劳便团往酸历市克何除消构府称太准精" +
            "值号率族维划选标写存候毛亲快效斯院查江型眼王按格养易置派层片始却专状育厂京识适属" +
            "圆包火住调满县局照参红细引听该铁价严龙飞" +
            "，。！？：；“”‘’（）【】《》、·—…％＃＠＆＊＋－／＝＜＞｜￥×÷±°≤≥≠∞←↑→↓●○■□◆◇▲▼★☆" +
            "０１２３４５６７８９ＡＢＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺ" +
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private const BindingFlags PubStatic =
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;

        internal static void Install()
        {
            var log = ZhPlugin.Log;
            try
            {
                var osFont = FindOsFont(log);
                var notes = new List<string>();

                // A. 按系统字体族名
                foreach (var fam in Families)
                {
                    foreach (var st in Styles)
                    {
                        _cjk = TryCreate("A:族名", new object[] { fam, st, 64 }, notes);
                        if (_cjk != null) { log.LogInfo("字体资产来源: CreateFontAsset(族名) " + fam + "/" + st); goto done; }
                    }
                }

                // B. 按字体文件路径
                foreach (var fp in FontFiles)
                {
                    if (!File.Exists(fp)) continue;
                    _cjk = TryCreate("B:文件(无Dynamic)", new object[]
                        { fp, 0, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024 }, notes);
                    if (_cjk == null)
                        _cjk = TryCreate("B:文件(Dynamic)", new object[]
                            { fp, 0, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true }, notes);
                    if (_cjk != null) { log.LogInfo("字体资产来源: 字体文件 " + fp); goto done; }
                }

                // C. 按 Font 对象
                if (osFont != null)
                {
                    _cjk = TryCreate("C:Font(8参)", new object[]
                        { osFont, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true }, notes);
                    if (_cjk == null)
                        _cjk = TryCreate("C:Font(单参)", new object[] { osFont }, notes);
                    if (_cjk != null) log.LogInfo("字体资产来源: Font 对象");
                }

            done:
                if (_cjk == null)
                {
                    Status = "三条策略全部失败";
                    foreach (var n in notes) log.LogError("  字体尝试: " + n);
                    return;
                }

                EnsureDynamic();
                WarmUp(log);
                RegisterAsFallback(log);

                int n2 = _cjk.characterTable != null ? _cjk.characterTable.Count : 0;
                Status = string.Format("{0} (mode={1}, 已注册字形 {2})",
                    _cjk.name, _cjk.atlasPopulationMode, n2);
            }
            catch (Exception e)
            {
                Status = "字体安装异常: " + e.Message;
                log.LogError("FontFix: " + e);
            }
        }

        private static TMP_FontAsset TryCreate(string tag, object[] args, List<string> notes)
        {
            var t = typeof(TMP_FontAsset);
            foreach (var m in t.GetMethods(PubStatic))
            {
                if (m.Name != "CreateFontAsset") continue;
                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                try
                {
                    var r = m.Invoke(null, args);
                    var fa = r as TMP_FontAsset;
                    if (fa != null) return fa;
                    notes.Add(tag + " -> 返回 null");
                }
                catch (TargetInvocationException tie)
                {
                    notes.Add(tag + " -> " + (tie.InnerException != null
                        ? tie.InnerException.GetType().Name + ": " + tie.InnerException.Message
                        : tie.Message));
                }
                catch (Exception e)
                {
                    notes.Add(tag + " -> " + e.GetType().Name + ": " + e.Message);
                }
            }
            return null;
        }

        private static void EnsureDynamic()
        {
            try
            {
                if (_cjk.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                    _cjk.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            }
            catch (Exception e) { ZhPlugin.Log.LogWarning("设 Dynamic 失败: " + e.Message); }
        }

        private static Font FindOsFont(BepInEx.Logging.ManualLogSource log)
        {
            foreach (var name in Families)
            {
                try
                {
                    var f = Font.CreateDynamicFontFromOSFont(name, 64);
                    if (f == null) continue;
                    if (!f.HasCharacter('汉') || !f.HasCharacter('中')) continue;
                    log.LogInfo("系统字体可用: " + name);
                    return f;
                }
                catch { }
            }
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                if (installed != null)
                    foreach (var name in installed)
                    {
                        try
                        {
                            var f = Font.CreateDynamicFontFromOSFont(name, 64);
                            if (f != null && f.HasCharacter('汉') && f.HasCharacter('中'))
                            {
                                log.LogInfo("回退选中系统字体: " + name);
                                return f;
                            }
                        }
                        catch { }
                    }
            }
            catch (Exception e) { log.LogWarning("枚举系统字体失败: " + e.Message); }
            return null;
        }

        private static void WarmUp(BepInEx.Logging.ManualLogSource log)
        {
            try
            {
                var ms = typeof(TMP_FontAsset).GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var m in ms)
                {
                    if (m.Name != "TryAddCharacters") continue;
                    var ps = m.GetParameters();
                    try
                    {
                        if (ps.Length == 2 && ps[0].ParameterType == typeof(string)
                            && ps[1].ParameterType == typeof(bool))
                        {
                            m.Invoke(_cjk, new object[] { Prewarm, false });
                            return;
                        }
                        if (ps.Length == 3 && ps[1].ParameterType == typeof(string).MakeByRefType())
                        {
                            m.Invoke(_cjk, new object[] { Prewarm, null, false });
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        log.LogWarning("TryAddCharacters 变体失败: " + e.Message);
                    }
                }
                log.LogWarning("未找到可用的 TryAddCharacters 重载");
            }
            catch (Exception e) { log.LogWarning("字形预热失败: " + e.Message); }
        }

        private static void RegisterAsFallback(BepInEx.Logging.ManualLogSource log)
        {
            int n = 0;
            try
            {
                var list = TMP_Settings.fallbackFontAssets;
                if (list != null && !list.Contains(_cjk)) { list.Add(_cjk); n++; }
            }
            catch (Exception e) { log.LogWarning("写全局回退表失败: " + e.Message); }

            try
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (all != null)
                    foreach (var fa in all)
                    {
                        if (fa == null || fa == _cjk) continue;
                        try
                        {
                            if (fa.fallbackFontAssetTable == null)
                                fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                            if (!fa.fallbackFontAssetTable.Contains(_cjk))
                            {
                                fa.fallbackFontAssetTable.Add(_cjk);
                                n++;
                            }
                        }
                        catch { }
                    }
            }
            catch (Exception e) { log.LogWarning("写字体资产回退表失败: " + e.Message); }

            log.LogInfo("已挂载到 " + n + " 处回退位置");
        }
    }
}
