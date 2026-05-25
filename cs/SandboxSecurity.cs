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
        "System.GC", "System.Delegate", "System.MulticastDelegate"
    };

    public static (bool IsSafe, string ErrorFeedback) AnalyzeUserCode(SyntaxTree tree, SemanticModel semanticModel, bool isValidationCode = false)
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

            // correctly resolve the type name whether the symbol is a type itself or a member of a type
            string fullTypeName = symbol is ITypeSymbol ? (symbol.ToString() ?? "") : (symbol.ContainingType?.ToString() ?? "");

            // fallback if it's a namespace or unresolved object
            if (string.IsNullOrEmpty(fullTypeName))
            {
                fullTypeName = symbol.ToString() ?? "";
            }

            // allow specific reflection and instantiation for VALIDATION CODE ONLY
            bool bypassReflection = isValidationCode && (
                fullNamespace == "System.Reflection" ||
                fullNamespace.StartsWith("System.Reflection.") ||
                fullTypeName == "System.Type" ||
                fullTypeName == "System.Activator"
            );

            // check namespace blocklist
            if (!bypassReflection && BlockedNamespaces.Any(b => fullNamespace == b || fullNamespace.StartsWith(b + ".")))
            {
                return (false, $"Sicherheitsrisiko: Der Zugriff auf '{fullNamespace}' ist blockiert.");
            }

            // check explicit type blocklist
            if (!bypassReflection && (BlockedTypes.Contains(fullTypeName) || BlockedTypes.Contains(symbol.ToString())))
            {
                return (false, $"Sicherheitsrisiko: Die Verwendung von '{fullTypeName}' ist blockiert.");
            }

            // safe 'Console.Write/Line()' whitelisting
            if (fullTypeName == "System.Console")
            {
                if (symbol is IMethodSymbol methodSym)
                {
                    if (methodSym.Name != "Write" && methodSym.Name != "WriteLine")
                    {
                        return (false, $"Sicherheitsrisiko: Console.{methodSym.Name} ist blockiert. Nur Write und WriteLine sind erlaubt.");
                    }
                }
                else if (symbol is IPropertySymbol)
                {
                    return (false, "Sicherheitsrisiko: Der direkte Zugriff auf Eigenschaften von System.Console ist blockiert.");
                }
            }

            // --- DEEP EXPLOIT PREVENTION FOR VALIDATION CODE ---
            // even though validation code is allowed to use reflection, we must trap any attempt to reflect on core framework libraries
            if (isValidationCode)
            {
                if (symbol is IMethodSymbol methodSymbol)
                {
                    // block 'Type.GetType(...)' to prevent dynamically resolving blocked classes
                    if (methodSymbol.Name == "GetType" && methodSymbol.ContainingType?.ToString() == "System.Type")
                    {
                        return (false, "Sicherheitsrisiko: Type.GetType() ist blockiert, um Reflection-Bypasses zu verhindern.");
                    }

                    // block fetching system assemblies like 'Assembly.GetExecutingAssembly()'
                    if (methodSymbol.Name.EndsWith("Assembly") && methodSymbol.Name.StartsWith("Get") && methodSymbol.ContainingType?.ToString() == "System.Reflection.Assembly")
                    {
                        return (false, $"Sicherheitsrisiko: {methodSymbol.Name}() ist blockiert.");
                    }

                    // block 'Assembly.Load(...)' 
                    if (methodSymbol.Name.StartsWith("Load") && methodSymbol.ContainingType?.ToString() == "System.Reflection.Assembly")
                    {
                        return (false, "Sicherheitsrisiko: Das manuelle Laden von Assemblies ist blockiert.");
                    }

                    // block 'Activator.CreateInstance(string, string)' to prevent instantiating framework classes by string
                    if (methodSymbol.Name == "CreateInstance" && methodSymbol.ContainingType?.ToString() == "System.Activator")
                    {
                        if (methodSymbol.Parameters.Length > 0 && methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String)
                        {
                            return (false, "Sicherheitsrisiko: Activator.CreateInstance mit String-Parametern ist blockiert.");
                        }
                    }
                }
                else if (symbol is IPropertySymbol propSymbol)
                {
                    // block 'Type.Assembly' property, this is vital!
                    // it prevents attackers from doing: 'typeof(string).Assembly.GetType("System.IO.File")'
                    if (propSymbol.Name == "Assembly" && propSymbol.ContainingType?.ToString() == "System.Type")
                    {
                        return (false, "Sicherheitsrisiko: Der Zugriff auf Type.Assembly ist blockiert.");
                    }
                }
            }
        }

        return (true, "");
    }
}