using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalyzer;

internal sealed class CyclomaticComplexityWalker : CSharpSyntaxWalker
{
    private int _complexity = 1;

    public static int Calculate(SyntaxNode node)
    {
        var walker = new CyclomaticComplexityWalker();
        walker.Visit(node);
        return walker._complexity;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        _complexity++;
        base.VisitIfStatement(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        _complexity++;
        base.VisitForStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        _complexity++;
        base.VisitForEachStatement(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        _complexity++;
        base.VisitWhileStatement(node);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        _complexity++;
        base.VisitDoStatement(node);
    }

    public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
    {
        _complexity++;
        base.VisitCaseSwitchLabel(node);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        _complexity++;
        base.VisitCatchClause(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        _complexity++;
        base.VisitConditionalExpression(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression))
        {
            _complexity++;
        }

        base.VisitBinaryExpression(node);
    }
}
