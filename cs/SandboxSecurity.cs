using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace AbiturEliteCode.cs;

public static class SandboxSecurity
{
    // list of explicitly blocked namespaces and types
    private static readonly string[] BlockedNamespaces = {
        "System.IO", "System.Net", "System.Reflection", "System.Diagnostics",
        "System.Runtime", "System.Threading", "System.Security", "Microsoft.Win32"
    };
    private static readonly string[] BlockedTypes = {
        "System.Type", "System.AppDomain", "System.Activator", "System.Environment",
        "System.GC", "System.Console", "System.Delegate", "System.MulticastDelegate"
    };

    public static (bool IsSafe, string ErrorFeedback) AnalyzeUserCode(SyntaxTree tree, SemanticModel semanticModel)
    {
        var root = tree.GetRoot();

        // prevent unsafe blocks at the syntax level
        if (root.DescendantNodes().OfType<UnsafeStatementSyntax>().Any())
        {
            return (false, "Sicherheitsrisiko: 'unsafe' Code ist in Custom Levels nicht erlaubt.");
        }

        // semantic analysis of every identifier and method call
        var nodesToCheck = root.DescendantNodes().Where(n =>
            n is IdentifierNameSyntax ||
            n is MemberAccessExpressionSyntax ||
            n is InvocationExpressionSyntax ||
            n is ObjectCreationExpressionSyntax);

        foreach (var node in nodesToCheck)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;

            if (symbol == null) continue;

            string fullNamespace = symbol.ContainingNamespace?.ToString() ?? "";
            string fullTypeName = symbol.ContainingType?.ToString() ?? "";

            // check namespace blocklist
            if (BlockedNamespaces.Any(b => fullNamespace == b || fullNamespace.StartsWith(b + ".")))
            {
                return (false, $"Sicherheitsrisiko: Der Zugriff auf '{fullNamespace}' ist blockiert.");
            }

            // check explicit type blocklist
            if (BlockedTypes.Contains(fullTypeName) || BlockedTypes.Contains(symbol.ToString()))
            {
                return (false, $"Sicherheitsrisiko: Die Verwendung von '{fullTypeName}' ist blockiert.");
            }
        }

        return (true, "");
    }
}