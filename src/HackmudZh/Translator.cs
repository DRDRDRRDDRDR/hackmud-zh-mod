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
            _phrases = new List<KeyValuePair<string, string>>();
            foreach (var kv in Dict)
            {
                if (!IsPhraseSafe(kv.Key)) continue;
                _phrases.Add(kv);
            }
            _phrases.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            LineMemo.Clear();
            WholeMemo.Clear();

            ZhPlugin.Log.LogInfo(string.Format(
                "词典 {0} 条（其中 {1} 条可参与短语替换，其余仅整串匹配）",
                Dict.Count, _phrases.Count));
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
            if (Dict.TryGetValue(line, out exact))
            {
                outp = exact;
            }
            else
            {
                outp = ReplacePhrasesTagAware(line);
            }

            // 缓存上限，防止长会话无限增长
            if (LineMemo.Count > 20000) LineMemo.Clear();
            LineMemo[line] = outp;
            return outp;
        }

        /// <summary>
        /// 标签感知的短语替换。
        ///
        /// 为什么需要：客户端的语法高亮会把脚本名/用户名包进 &lt;color=...&gt;，
        /// 于是 `Top level marks are available…` 在屏幕上变成
        /// `Top level &lt;color=…&gt;marks&lt;/color&gt; are available…`，
        /// 整串与词典 key 对不上。这里先在**去掉标签的视图**上做匹配，
        /// 再把命中的区间映射回原文（含标签），因此**颜色标签完整保留**。
        /// </summary>
        private static string ReplacePhrasesTagAware(string line)
        {
            var plain = new StringBuilder(line.Length);
            var map = new List<int>(line.Length);   // plain 下标 -> 原文下标
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '<')
                {
                    int close = line.IndexOf('>', i);
                    if (close > i && close - i <= 42 && IsRichTag(line, i, close))
                    {
                        i = close + 1;
                        continue;
                    }
                }
                plain.Append(line[i]);
                map.Add(i);
                i++;
            }

            string p = plain.ToString();
            var sb = new StringBuilder(line.Length + 32);
            int src = 0;       // 原文游标
            int k = 0;         // plain 游标
            while (k < p.Length)
            {
                int bestLen = 0, bestIdx = -1;
                for (int q = 0; q < _phrases.Count; q++)
                {
                    var key = _phrases[q].Key;
                    if (key.Length == 0 || key.Length > p.Length - k) continue;
                    if (p[k] != key[0]) continue;
                    if (string.CompareOrdinal(p, k, key, 0, key.Length) != 0) continue;
                    if (!BoundaryOk(p, k, key)) continue;
                    bestLen = key.Length; bestIdx = q;
                    break;      // _phrases 已按长度降序
                }
                if (bestIdx < 0)
                {
                    // 未命中：原样输出原文这一段（含其中的标签）
                    int nextOrig = (k < map.Count) ? map[k] : line.Length;
                    sb.Append(line, src, nextOrig - src);
                    src = nextOrig;
                    if (k < map.Count) { sb.Append(line[src]); src++; }
                    k++;
                }
                else
                {
                    int startOrig = map[k];
                    int endPlain = k + bestLen - 1;
                    int endOrig = (endPlain + 1 < map.Count) ? map[endPlain + 1] : line.Length;
                    sb.Append(_phrases[bestIdx].Value);
                    src = endOrig;
                    k += bestLen;
                }
            }
            sb.Append(line, src, line.Length - src);
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
