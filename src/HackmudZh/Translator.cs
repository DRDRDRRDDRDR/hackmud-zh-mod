using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace HackmudZh
{
    /// <summary>
    /// 渲染层翻译引擎。
    ///
    /// 为什么不是「整串查表」：
    ///   set_text 拿到的往往是**整个终端缓冲区（多行）**，而且客户端大量用字符串拼接
    ///   （例如 "..." + 用户名 + " to continue."），所以词典里既有完整串也有**片段**。
    ///   因此引擎分三层：
    ///     1. 整串精确匹配（按钮/标签这类短文本）
    ///     2. 逐行处理：先精确匹配，再按**最长优先**做短语替换（处理拼接结果）
    ///     3. 行级记忆缓存（终端缓冲区每次都重设，缓存让重复行 O(1)）
    ///
    /// 安全边界：只处理含 ASCII 字母的串；已含中文的行不碰；空串/null 直接返回。
    /// </summary>
    internal static class Translator
    {
        private static readonly Dictionary<string, string> Dict =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>按长度降序的短语表（最长优先匹配，避免短词吃掉长词）</summary>
        private static List<KeyValuePair<string, string>> _phrases = new List<KeyValuePair<string, string>>();

        /// <summary>行级记忆缓存，避免终端缓冲区反复重算</summary>
        private static readonly Dictionary<string, string> LineMemo =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> WholeMemo =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>归一化（空白压平）后的整串索引 —— 对付对齐宽度不同与折行导致的整串不匹配</summary>
        private static readonly Dictionary<string, string> _normWhole =
            new Dictionary<string, string>(StringComparer.Ordinal);

        internal static int Count { get { return Dict.Count; } }
        internal static string Source = "(未加载)";

        /// <summary>按 JSON 文件路径直接加载（供自动化验收门 zhcheck 使用，与运行时同一份引擎）。</summary>
        internal static void LoadFrom(string jsonPath)
        {
            ParseJson(File.ReadAllText(jsonPath, Encoding.UTF8));
            Source = jsonPath;
            Rebuild();
        }

        internal static void Load(string pluginPath)
        {
            // 1) 外部 JSON（可热更新）优先
            try
            {
                var dir = Path.GetDirectoryName(pluginPath);
                var ext = Path.Combine(dir, "zh.json");
                if (File.Exists(ext))
                {
                    ParseJson(File.ReadAllText(ext, Encoding.UTF8));
                    Source = ext;
                    Rebuild();
                    return;
                }
            }
            catch (Exception e)
            {
                ZhPlugin.Log.LogWarning("外部词典读取失败，回退到内嵌词典: " + e.Message);
            }

            // 2) 内嵌词典
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("HackmudZh.zh.json"))
                {
                    if (s != null)
                    {
                        using (var r = new StreamReader(s, Encoding.UTF8))
                            ParseJson(r.ReadToEnd());
                        Source = "(内嵌)";
                    }
                }
            }
            catch (Exception e)
            {
                ZhPlugin.Log.LogError("内嵌词典读取失败: " + e.Message);
            }
            Rebuild();
        }

        private static void Rebuild()
        {
            // 短语表**只收含空格或标点的条目**。
            // 理由：裸单词（from / amount / confirm / time …）若参与子串替换，
            // 会误伤无关文本（例如 transform 里的 "from"）。裸单词只走整串精确匹配。
            //
            // key 一并做**空白归一化**：连续空白压成一个空格。
            // 因为客户端会用与源码不同的对齐宽度重排（见下方 ReplacePhrasesNormalized 的说明），
            // 不归一化的话 "help           [see this again]" 永远对不上。
            _phrases = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in Dict)
            {
                if (!IsPhraseSafe(kv.Key)) continue;
                var nk = Normalize(kv.Key);
                if (nk.Length < 3 || !seen.Add(nk)) continue;
                _phrases.Add(new KeyValuePair<string, string>(nk, kv.Value));
            }
            _phrases.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            LineMemo.Clear();
            WholeMemo.Clear();

            // 归一化整串索引：整条词条的空白压平后建索引，
            // 这样「源码对齐宽度」与「屏幕对齐宽度」不同时仍能整串命中。
            _normWhole.Clear();
            foreach (var kv in Dict)
            {
                var nk = Normalize(kv.Key);
                if (nk.Length >= 3 && !_normWhole.ContainsKey(nk)) _normWhole[nk] = kv.Value;
            }

            ZhPlugin.Log.LogInfo(string.Format(
                "词典 {0} 条（其中 {1} 条可参与短语替换，{2} 条走归一化整串匹配）",
                Dict.Count, _phrases.Count, _normWhole.Count));
        }

        /// <summary>空白归一化：连续空白（含 \n \t）压成一个空格，并 trim 两端。</summary>
        internal static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            bool ws = false, started = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f' || c == '\v')
                {
                    ws = true;
                    continue;
                }
                if (ws && started) sb.Append(' ');
                ws = false;
                started = true;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>含空格或标点的条目才允许参与子串替换。</summary>
        private static bool IsPhraseSafe(string k)
        {
            if (k.Length < 3) return false;
            for (int i = 0; i < k.Length; i++)
            {
                char c = k[i];
                if (c == ' ' || c == '\t' || c == '-' || c == '.' || c == ':' || c == '!' ||
                    c == '?' || c == ',' || c == ';' || c == '(' || c == ')' || c == '[' ||
                    c == ']' || c == '{' || c == '}' || c == '\'' || c == '"' || c == '/' ||
                    c == '<' || c == '>' || c == '=' || c == '|' || c == '_' || c == '\n')
                    return true;
            }
            return false;
        }

        /// <summary>供外部调用（也便于单测）</summary>
        internal static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (Dict.Count == 0) return text;
            if (!HasAsciiLetter(text)) return text;

            string hit;
            if (WholeMemo.TryGetValue(text, out hit)) return hit;

            string result;
            if (Dict.TryGetValue(text, out result))
            {
                WholeMemo[text] = result;
                return result;
            }

            // 多行：逐行处理
            if (text.IndexOf('\n') >= 0)
            {
                var lines = text.Split('\n');
                var sb = new StringBuilder(text.Length + 64);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0) sb.Append('\n');
                    sb.Append(TranslateLine(lines[i]));
                }
                result = sb.ToString();
            }
            else
            {
                result = TranslateLine(text);
            }

            WholeMemo[text] = result;
            return result;
        }

        private static string TranslateLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            if (!HasAsciiLetter(line)) return line;

            string memo;
            if (LineMemo.TryGetValue(line, out memo)) return memo;

            string exact;
            string outp;
            string nexact;
            if (Dict.TryGetValue(line, out exact))
            {
                outp = exact;
            }
            else if (_normWhole.TryGetValue(Normalize(line), out nexact))
            {
                outp = nexact;
            }
            else
            {
                outp = ReplacePhrasesNormalized(line);
            }

            // 缓存上限，防止长会话无限增长
            if (LineMemo.Count > 20000) LineMemo.Clear();
            LineMemo[line] = outp;
            return outp;
        }

        /// <summary>
        /// 归一化感知的短语替换 —— 同时解决三个问题。
        ///
        /// 1) **颜色标签**：客户端语法高亮把脚本名/用户名包进 &lt;color=…&gt;，
        ///    于是 `Top level marks are available…` 在屏幕上变成
        ///    `Top level &lt;color=…&gt;marks&lt;/color&gt; are available…`，整串对不上。
        /// 2) **对齐宽度不同**：客户端会用与源码不同的对齐宽度重排，
        ///    例如源码里 `help` 后跟 13 个空格，屏幕上只有 11 个。
        /// 3) **终端折行**：AddOutput 调 MEGNKIOGEBH(line, char_width) 按宽度折行，
        ///    超过一行宽度的长句在屏幕上被断成两行（中间是 \n）。
        ///
        /// 做法：构造一个「跳过标签、连续空白压成一个空格」的归一化视图，
        /// 同时记录每个归一化字符对应的**原文区间**；在归一化视图上匹配，
        /// 命中后把原文区间整体替换掉。因此标签与空白的差异都不再影响匹配。
        /// </summary>
        private static string ReplacePhrasesNormalized(string line)
        {
            int n = line.Length;
            var norm = new StringBuilder(n);
            var mapStart = new List<int>(n);   // 归一化下标 -> 原文起始
            var mapEnd = new List<int>(n);     // 归一化下标 -> 原文结束（不含）

            int i = 0;
            while (i < n)
            {
                char c = line[i];
                if (c == '<')
                {
                    int close = line.IndexOf('>', i);
                    if (close > i && close - i <= 42 && IsRichTag(line, i, close))
                    {
                        i = close + 1;
                        continue;
                    }
                }
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    int j = i;
                    while (j < n && (line[j] == ' ' || line[j] == '\t' || line[j] == '\n' || line[j] == '\r')) j++;
                    norm.Append(' ');
                    mapStart.Add(i);
                    mapEnd.Add(j);
                    i = j;
                    continue;
                }
                norm.Append(c);
                mapStart.Add(i);
                mapEnd.Add(i + 1);
                i++;
            }

            string p = norm.ToString();
            var sb = new StringBuilder(line.Length + 32);
            int src = 0, k = 0;
            while (k < p.Length)
            {
                int hitIdx = -1;
                for (int q = 0; q < _phrases.Count; q++)
                {
                    var key = _phrases[q].Key;
                    if (key.Length == 0 || key.Length > p.Length - k) continue;
                    if (p[k] != key[0]) continue;
                    if (string.CompareOrdinal(p, k, key, 0, key.Length) != 0) continue;
                    if (!BoundaryOk(p, k, key)) continue;
                    hitIdx = q;
                    break;              // _phrases 已按长度降序
                }

                if (hitIdx < 0)
                {
                    // 未命中：把原文这一段（含其中的标签/空白）原样输出
                    int s0 = mapStart[k], e0 = mapEnd[k];
                    if (s0 > src) sb.Append(line, src, s0 - src);
                    sb.Append(line, s0, e0 - s0);
                    src = e0;
                    k++;
                }
                else
                {
                    int keyLen = _phrases[hitIdx].Key.Length;
                    int s0 = mapStart[k];
                    int e0 = mapEnd[k + keyLen - 1];
                    if (s0 > src) sb.Append(line, src, s0 - src);
                    sb.Append(_phrases[hitIdx].Value);
                    src = e0;
                    k += keyLen;
                }
            }
            if (src < line.Length) sb.Append(line, src, line.Length - src);
            return sb.ToString();
        }

        /// <summary>判断 &lt;...&gt; 是不是富文本标签（而非普通尖括号内容）。</summary>
        private static bool IsRichTag(string s, int open, int close)
        {
            if (s[open] != '<' || s[close] != '>') return false;
            int j = open + 1;
            if (j < close && s[j] == '/') j++;
            if (j >= close) return false;
            char c = s[j];
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) return false;
            for (int t = j; t < close; t++)
            {
                char d = s[t];
                if (d == ' ' || d == '=' || d == '"' || d == '#' || d == '/' ||
                    (d >= 'a' && d <= 'z') || (d >= 'A' && d <= 'Z') || (d >= '0' && d <= '9'))
                    continue;
                return false;
            }
            return true;
        }

        /// <summary>最长优先的短语替换：一路找最长匹配，命中就替换并跳过。
        /// 带词边界校验 —— 短语首尾若是字母/数字，则要求原文对应位置也是词边界，
        /// 避免 "from" 命中 "transform" 这类误伤。</summary>
        private static string ReplacePhrases(string line)
        {
            var sb = new StringBuilder(line.Length + 32);
            int i = 0;
            while (i < line.Length)
            {
                bool matched = false;
                for (int p = 0; p < _phrases.Count; p++)
                {
                    var k = _phrases[p].Key;
                    if (k.Length == 0 || k.Length > line.Length - i) continue;
                    if (line[i] != k[0]) continue;
                    if (string.CompareOrdinal(line, i, k, 0, k.Length) != 0) continue;
                    if (!BoundaryOk(line, i, k)) continue;
                    sb.Append(_phrases[p].Value);
                    i += k.Length;
                    matched = true;
                    break;
                }
                if (!matched)
                {
                    sb.Append(line[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static bool IsWordChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }

        /// <summary>短语首尾是词字符时，要求原文外侧不是词字符。</summary>
        private static bool BoundaryOk(string line, int start, string k)
        {
            if (IsWordChar(k[0]))
            {
                if (start > 0 && IsWordChar(line[start - 1])) return false;
            }
            char last = k[k.Length - 1];
            if (IsWordChar(last))
            {
                int after = start + k.Length;
                if (after < line.Length && IsWordChar(line[after])) return false;
            }
            return true;
        }

        private static bool HasAsciiLetter(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return true;
            }
            return false;
        }

        // ---------------- 极简 JSON 解析（扁平 { "k": "v" }，无需第三方依赖） ----------------

        private static void ParseJson(string json)
        {
            int i = 0, n = json.Length;
            SkipWs(json, ref i);
            if (i >= n || json[i] != '{') throw new FormatException("词典不是 JSON 对象");
            i++;
            while (true)
            {
                SkipWs(json, ref i);
                if (i >= n) break;
                if (json[i] == '}') { i++; break; }
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '"') { i++; continue; }
                string key = ReadString(json, ref i);
                SkipWs(json, ref i);
                if (i < n && json[i] == ':') i++;
                SkipWs(json, ref i);
                if (i >= n || json[i] != '"') continue;
                string val = ReadString(json, ref i);
                if (key.Length > 0 && val.Length > 0) Dict[key] = val;
            }
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // 开引号
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            int code;
                            if (int.TryParse(s.Substring(i, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }
    }
}
