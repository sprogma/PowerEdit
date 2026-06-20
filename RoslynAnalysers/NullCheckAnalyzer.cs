using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynAnalysers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NullCheckAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MY0001";

#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use pattern matching for null checks",
        "Use 'is null' or 'is not null' pattern matching instead of equality operators",
        "Style",
        DiagnosticSeverity.Info, // Показывается как Note (информационная точка)
        isEnabledByDefault: true);
#pragma warning restore RS2008

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Слушаем и равенство (==), и неравенство (!=)
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        // Проверяем, является ли левая или правая часть null
        bool isLeftNull = binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression);
        bool isRightNull = binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression);

        if (isLeftNull || isRightNull)
        {
            var diagnostic = Diagnostic.Create(Rule, binaryExpression.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
