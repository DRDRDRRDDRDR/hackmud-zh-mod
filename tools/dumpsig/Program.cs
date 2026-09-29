using System;
using System.Linq;
using Mono.Cecil;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dumpsig <assembly.dll> <TypeName> [成员名过滤]");
            return;
        }
        var asm = AssemblyDefinition.ReadAssembly(args[0]);
        string typeName = args[1];
        string filter = args.Length > 2 ? args[2] : null;

        foreach (var mod in asm.Modules)
        {
            foreach (var t in mod.GetTypes())
            {
                if (t.FullName != typeName && t.Name != typeName) continue;
                Console.WriteLine("=== " + t.FullName + " ===");
                foreach (var m in t.Methods)
                {
                    if (filter != null && !m.Name.Contains(filter)) continue;
                    Console.WriteLine("  " + Sig(m));
                }
                foreach (var p in t.Properties)
                {
                    if (filter != null && !p.Name.Contains(filter)) continue;
                    Console.WriteLine("  PROP " + p.PropertyType.FullName + " " + p.Name
                        + " { " + (p.GetMethod != null ? "get; " : "") + (p.SetMethod != null ? "set; " : "") + "}");
                }
                foreach (var f in t.Fields)
                {
                    if (filter != null && !f.Name.Contains(filter)) continue;
                    Console.WriteLine("  FIELD " + (f.IsStatic ? "static " : "") + f.FieldType.FullName + " " + f.Name);
                }
            }
        }
    }

    static string Sig(MethodDefinition m)
    {
        var ps = string.Join(", ", m.Parameters.Select(p =>
            (p.ParameterType.IsByReference ? "ref " : "") + p.ParameterType.FullName + " " + p.Name));
        string mods = "";
        if (m.IsPublic) mods += "public ";
        if (m.IsStatic) mods += "static ";
        return mods + m.ReturnType.FullName + " " + m.Name + "(" + ps + ")";
    }
}
