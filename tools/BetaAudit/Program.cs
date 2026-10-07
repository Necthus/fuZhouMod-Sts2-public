using Mono.Cecil;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: BetaAudit assembly.dll search-directory [...]");
    return 2;
}
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
foreach (string directory in args.Skip(1)) resolver.AddSearchDirectory(directory);
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
using var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
int failures = 0, references = 0, overrides = 0;
var gameAssemblies = new HashSet<string> { "sts2", "BaseLib", "GodotSharp", "0Harmony" };
foreach (var member in module.GetMemberReferences())
{
    string? assembly = member.DeclaringType.Scope is AssemblyNameReference scope ? scope.Name : null;
    if (assembly == null || !gameAssemblies.Contains(assembly)) continue;
    references++;
    try
    {
        bool valid = member switch
        {
            MethodReference method => method.Resolve() != null,
            FieldReference field => field.Resolve() != null,
            _ => true
        };
        if (!valid) throw new MissingMemberException(member.FullName);
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"UNRESOLVED {member.FullName}: {ex.Message}");
    }
}

IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types) => types.SelectMany(t => new[] { t }.Concat(AllTypes(t.NestedTypes)));
foreach (var type in AllTypes(module.Types))
{
    foreach (var method in type.Methods.Where(m => m.IsVirtual && !m.IsNewSlot))
    {
        overrides++;
        bool found = false;
        var parent = type.BaseType;
        while (parent != null)
        {
            var definition = parent.Resolve();
            if (definition == null) break;
            found = definition.Methods.Any(m => m.IsVirtual && m.Name == method.Name
                && m.ReturnType.FullName == method.ReturnType.FullName
                && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(method.Parameters.Select(p => p.ParameterType.FullName)));
            if (found) break;
            parent = definition.BaseType;
        }
        if (!found)
        {
            failures++;
            Console.WriteLine($"INVALID OVERRIDE {method.FullName}");
        }
    }
}
Console.WriteLine($"Checked {references} game/library member references and {overrides} overrides; failures: {failures}.");
return failures == 0 ? 0 : 1;
