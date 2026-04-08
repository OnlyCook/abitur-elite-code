using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private class LoopGuardRewriter : CSharpSyntaxRewriter
    {
        private StatementSyntax GetCheckStatement()
        {
            return SyntaxFactory.ParseStatement("AbiturEliteCode.CodeGuard.Check();\n");
        }

        private BlockSyntax EnsureBlock(StatementSyntax statement)
        {
            if (statement is BlockSyntax block)
                return block.WithStatements(block.Statements.Insert(0, GetCheckStatement()));
            return SyntaxFactory.Block(GetCheckStatement(), statement);
        }

        public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
        {
            var visitedNode = (WhileStatementSyntax)base.VisitWhileStatement(node);

            var method = visitedNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method != null && (method.Identifier.Text.Equals("Run", StringComparison.OrdinalIgnoreCase) ||
                                   method.Identifier.Text.Equals("RunServer", StringComparison.OrdinalIgnoreCase)))
                // only inject return if it is explicitly an infinite loop
                if (visitedNode.Condition is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.TrueLiteralExpression))
                {
                    var returnStatement = SyntaxFactory.ParseStatement("return;\n");
                    var block = visitedNode.Statement is BlockSyntax b ? b : SyntaxFactory.Block(visitedNode.Statement);
                    block = block.AddStatements(returnStatement);
                    return visitedNode.WithStatement(EnsureBlock(block));
                }

            return visitedNode.WithStatement(EnsureBlock(visitedNode.Statement));
        }

        public override SyntaxNode VisitForStatement(ForStatementSyntax node)
        {
            var visitedNode = (ForStatementSyntax)base.VisitForStatement(node);

            var method = visitedNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method != null && (method.Identifier.Text.Equals("Run", StringComparison.OrdinalIgnoreCase) ||
                                   method.Identifier.Text.Equals("RunServer", StringComparison.OrdinalIgnoreCase)))
                // only inject return if it is an infinite for loop
                if (visitedNode.Condition == null)
                {
                    var returnStatement = SyntaxFactory.ParseStatement("return;\n");
                    var block = visitedNode.Statement is BlockSyntax b ? b : SyntaxFactory.Block(visitedNode.Statement);
                    block = block.AddStatements(returnStatement);
                    return visitedNode.WithStatement(EnsureBlock(block));
                }

            return visitedNode.WithStatement(EnsureBlock(visitedNode.Statement));
        }

        public override SyntaxNode VisitDoStatement(DoStatementSyntax node)
        {
            var visitedNode = (DoStatementSyntax)base.VisitDoStatement(node);
            return visitedNode.WithStatement(EnsureBlock(visitedNode.Statement));
        }
    }
}