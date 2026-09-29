using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HackmudZh;

/// <summary>
/// zhcheck —— 词典/翻译引擎的自动化验收门。
///
/// 拿**真实的渲染行**（来自客户端 shell.txt，含 &lt;color&gt; 标签，与 TMP_Text.set_text 收到的东西同构）
/// 跑一遍翻译，报告：
///   1. 仍含英文单词的行（按出现次数排序）—— 这是可执行的缺口清单
///   2. 词典命中率
/// 退出码 0 = 所有行都已无英文残留（或仅有允许清单内的）
/// </summary>
class Program
{
    // 允许残留的英文（命令名、脚本名、URL、合法术语等）——这里不做硬失败，只统计
    static readonly Regex WordRe = new Regex(@"[A-Za-z]{3,}", RegexOptions.Compiled);
    static readonly Regex TagRe = new Regex(@"</?color(?:=#[0-9A-Fa-f]{8})?>", RegexOptions.Compiled);
    static readonly Regex EscRe = new Regex(@"[\u00C0-\u00FF]{2,}", RegexOptions.Compiled);

    static int Main(string[] args)
    {
        string dict = args.Length > 0 ? args[0] : @"C:\Users\DR\Downloads\DSH\hackmud-zh-mod\dict\zh.json";
        string corpus = args.Length > 1 ? args[1] : null;

        Translator.LoadFrom(dict);
        Console.WriteLine("词典: " + dict + "  (" + Translator.Count + " 条)");
        Console.WriteLine();

        // --show 模式：逐行打印「输入 -> 输出」，用于定点验证
        bool show = args.Any(a => a == "--show");
        bool whole = args.Any(a => a == "--whole");
        if (show || whole)
        {
            var src = corpus != null && File.Exists(corpus) ? corpus : null;
            if (src == null) { Console.WriteLine("--show/--whole 需要指定语料文件"); return 2; }
            if (whole)
            {
                // 把整个文件当成**一段文本**传给引擎 —— 复现真机行为
                // （set_text 收到的是整个终端缓冲区，多行）。
                // 逐行读会漏掉「跨行才匹配得到」的 key，测不出跨折行效果。
                var all = File.ReadAllText(src, Encoding.UTF8);
                var tr = Translator.Translate(all);
                Console.WriteLine("=== 整段输入 ===");
                Console.WriteLine(all);
                Console.WriteLine("=== 整段输出 ===");
                Console.WriteLine(tr);
                return 0;
            }
            foreach (var raw in File.ReadAllLines(src, Encoding.UTF8))
            {
                if (raw.Trim().Length == 0) continue;
                var tr = Translator.Translate(raw);
                var didTr = tr != raw;
                Console.WriteLine("[" + (didTr ? "译" : "原") + "] " + raw);
                if (didTr)
                {
                    var plain = TagRe.Replace(tr, "");
                    Console.WriteLine("     -> " + plain.Trim());
                }
                Console.WriteLine();
            }
            return 0;
        }

        // 语料：默认收集 shell.txt 的全部行
        var lines = new List<string>();
        if (corpus != null && File.Exists(corpus))
        {
            lines.AddRange(File.ReadAllLines(corpus, Encoding.UTF8));
        }
        else
        {
            var appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hackmud");
            foreach (var f in new[] { "shell.txt" })
            {
                var p = Path.Combine(appdata, f);
                if (File.Exists(p)) lines.AddRange(File.ReadAllLines(p, Encoding.UTF8));
            }
        }
        lines = lines.Where(l => l.Trim().Length > 0).Distinct().ToList();
        Console.WriteLine("语料行数(去重): " + lines.Count);
        Console.WriteLine();

        int changed = 0, stillEn = 0;
        var leftovers = new Dictionary<string, int>();

        foreach (var raw in lines)
        {
            var line = raw;
            var outp = Translator.Translate(line);
            if (outp != line) changed++;

            // 去掉颜色标签后再看有没有英文单词
            var plain = TagRe.Replace(outp, " ");
            plain = EscRe.Replace(plain, " ");
            var words = WordRe.Matches(plain).Cast<Match>().Select(m => m.Value).ToList();
            if (words.Count > 0)
            {
                stillEn++;
                foreach (var w in words) leftovers[w] = leftovers.ContainsKey(w) ? leftovers[w] + 1 : 1;
            }
        }

        Console.WriteLine("被翻译的行 : " + changed + " / " + lines.Count);
        Console.WriteLine("仍含英文的行: " + stillEn);
        Console.WriteLine();
        Console.WriteLine("=== 高频残留英文词（前 60）===");
        foreach (var kv in leftovers.OrderByDescending(k => k.Value).Take(60))
            Console.WriteLine(string.Format("  {0,5}  {1}", kv.Value, kv.Key));

        // 把仍含英文的整行也写出来，便于直接补词条
        var outFile = Path.Combine(Path.GetDirectoryName(dict), "gaps.txt");
        var gapLines = new List<Tuple<int, string>>();
        foreach (var raw in lines)
        {
            var tr = Translator.Translate(raw);
            var plain = TagRe.Replace(tr, " ");
            plain = EscRe.Replace(plain, " ");
            int n = WordRe.Matches(plain).Count;
            if (n > 0) gapLines.Add(Tuple.Create(n, plain.Trim()));
        }
        using (var w = new StreamWriter(outFile, false, new UTF8Encoding(false)))
        {
            w.WriteLine("# 仍含英文的渲染行（已去颜色标签，按英文词数降序）");
            w.WriteLine("# 共 " + gapLines.Count + " 行 ｜ 生成 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            w.WriteLine("# 用法：把有意义的行译成中文，加进 dict/zh_supplement3.json 后跑 merge_dict.py");
            w.WriteLine();
            foreach (var t in gapLines.OrderByDescending(x => x.Item1))
            {
                w.WriteLine(t.Item2);
            }
        }
        Console.WriteLine();
        Console.WriteLine("缺口清单 -> " + outFile + "  (" + gapLines.Count + " 行)");
        return 0;
    }
}
