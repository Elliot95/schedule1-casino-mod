using System;
using System.IO;
using System.Linq;
using System.Reflection;

class Program
{
    static string[] _probeDirs;

    static int Main(string[] args)
    {
        string game = @"C:\Program Files (x86)\Steam\steamapps\common\Schedule I";
        _probeDirs = new[]
        {
            Path.Combine(game, @"MelonLoader\Il2CppAssemblies"),
            Path.Combine(game, @"MelonLoader\net6"),
        };

        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, e) => Resolve(e.Name);
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => Resolve(e.Name);

        var path = Path.Combine(_probeDirs[0], "Assembly-CSharp.dll");
        var asm = Assembly.LoadFrom(path);
        Console.WriteLine($"Loaded: {asm.GetName().Name}  (file {new FileInfo(path).LastWriteTime})");

        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
        Console.WriteLine($"Types readable: {types.Length}\n");

        foreach (var name in args.Length > 0 ? args : Defaults)
        {
            var t = types.FirstOrDefault(x => x.FullName == name)
                 ?? types.FirstOrDefault(x => x.Name == name);
            if (t == null) { Console.WriteLine($"### {name}: NOT FOUND\n"); continue; }
            Dump(t);
        }
        return 0;
    }

    static readonly string[] Defaults =
    {
        "Il2CppScheduleOne.Casino.CasinoGamePlayerData",
        "Il2CppScheduleOne.Casino.CasinoGamePlayers",
        "Il2CppScheduleOne.Casino.CasinoGameController",
        "Il2CppScheduleOne.Casino.CardController",
        "Il2CppScheduleOne.Money.MoneyManager",
    };

    static Assembly Resolve(string fullName)
    {
        var simple = fullName.Split(',')[0];
        foreach (var dir in _probeDirs)
        {
            var p = Path.Combine(dir, simple + ".dll");
            if (File.Exists(p))
            {
                try { return Assembly.LoadFrom(p); } catch { }
            }
        }
        return null;
    }

    static void Dump(Type t)
    {
        Console.WriteLine($"### {t.FullName}");
        Console.WriteLine($"    base: {t.BaseType?.FullName}");

        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
                             | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var m in t.GetMethods(F).Where(m => !m.IsSpecialName).OrderBy(m => m.Name))
        {
            // byref params need out/ref at the call site -- CS1620 if you guess wrong.
            var ps = string.Join(", ", m.GetParameters().Select(p =>
                $"{(p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : "")}{Short(p.ParameterType)} {p.Name}"));
            var gen = m.IsGenericMethodDefinition
                ? "<" + string.Join(",", m.GetGenericArguments().Select(a => a.Name)) + ">"
                : "";
            var mods = (m.IsStatic ? "static " : "") + (m.IsPublic ? "public " : m.IsFamily ? "protected " : "private ");
            Console.WriteLine($"    {mods}{Short(m.ReturnType)} {m.Name}{gen}({ps})");
        }

        foreach (var p in t.GetProperties(F).OrderBy(p => p.Name))
        {
            // Il2CppInterop turns const/static-readonly fields into STATIC properties. Missing
            // this produces CS0176 at build time, so it must be visible in the dump.
            var isStatic = p.GetAccessors(true).Any(a => a.IsStatic);
            var byref = p.PropertyType.IsByRef ? " (byref)" : "";
            Console.WriteLine($"    prop {(isStatic ? "static " : "")}{Short(p.PropertyType)} {p.Name}{byref} {{{(p.CanRead ? " get;" : "")}{(p.CanWrite ? " set;" : "")} }}");
        }

        foreach (var f in t.GetFields(F).Where(f => !f.Name.StartsWith("NativeFieldInfoPtr") && !f.Name.StartsWith("NativeMethodInfoPtr")).OrderBy(f => f.Name))
            Console.WriteLine($"    field {(f.IsStatic ? "static " : "")}{Short(f.FieldType)} {f.Name}");

        Console.WriteLine();
    }

    static string Short(Type t)
    {
        if (t == null) return "?";
        var n = t.Name;
        if (t.IsGenericType)
            n = n.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(Short)) + ">";
        return n;
    }
}
