using ComplexityAnalyzer;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: ComplexityAnalyzer <path> [--threshold <n>]");
    return 2;
}

var path = args[0];
var threshold = 10;
for (var i = 1; i < args.Length; i++)
{
    if (args[i] == "--threshold" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed))
    {
        threshold = parsed;
        i++;
    }
}

var results = Directory
    .EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
    .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
    .SelectMany(file =>
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
        return root.DescendantNodes()
            .OfType<BaseMethodDeclarationSyntax>()
            .Select(member => (
                File: Path.GetRelativePath(path, file),
                Member: MemberName(member),
                Complexity: CyclomaticComplexityWalker.Calculate(member)));
    })
    .OrderByDescending(result => result.Complexity)
    .ToList();

foreach (var result in results)
{
    Console.WriteLine($"{result.Complexity,4}  {result.File}::{result.Member}");
}

var violations = results.Where(result => result.Complexity > threshold).ToList();
if (violations.Count == 0)
{
    return 0;
}

Console.Error.WriteLine();
Console.Error.WriteLine($"Cyclomatic complexity exceeds threshold ({threshold}) in {violations.Count} member(s):");
foreach (var violation in violations)
{
    Console.Error.WriteLine($"  {violation.File}::{violation.Member} = {violation.Complexity}");
}

return 1;

static string MemberName(BaseMethodDeclarationSyntax member) => member switch
{
    MethodDeclarationSyntax m => m.Identifier.Text,
    ConstructorDeclarationSyntax c => $"{c.Identifier.Text} (ctor)",
    _ => member.Kind().ToString(),
};
