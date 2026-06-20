using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting; // Обязательный namespace из документации
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RoslynAnalysers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullCheckCodeFixProvider)), Shared]
public class NullCheckCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(NullCheckAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Строго по документации: берем токен на старте диапазона и ищем предка нужного типа
        var token = root.FindToken(diagnosticSpan.Start);
        var binaryExpression = token.Parent.AncestorsAndSelf().OfType<BinaryExpressionSyntax>().FirstOrDefault();

        if (binaryExpression == null) return;

        string title = binaryExpression.IsKind(SyntaxKind.EqualsExpression)
            ? "Replace with 'is null'"
            : "Replace with 'is not null'";

        // Регистрируем Code Action строго по гайду Microsoft
        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => ReplaceWithPatternMatchingAsync(context.Document, binaryExpression, c),
                equivalenceKey: title),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithPatternMatchingAsync(Document document,
        BinaryExpressionSyntax binaryExpression,
        CancellationToken cancellationToken)
    {
        // 1. Выделяем проверяемое выражение (переменную)
        var expressionToCheck = binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression)
            ? binaryExpression.Right
            : binaryExpression.Left;

        // Извлекаем тривии (пробелы/отступы) из оригинального выражения, как в гайде с const
        SyntaxToken firstToken = binaryExpression.GetFirstToken();
        SyntaxTriviaList leadingTrivia = firstToken.LeadingTrivia;

        // 2. Строим шаблон
        var nullPattern = SyntaxFactory.ConstantPattern(
            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

        PatternSyntax finalPattern;

        if (binaryExpression.IsKind(SyntaxKind.EqualsExpression))
        {
            finalPattern = nullPattern;
        }
        else
        {
            // 'not null' паттерн с эластичным маркером для правильных отступов
            var notToken = SyntaxFactory.Token(
                SyntaxFactory.TriviaList(),
                SyntaxKind.NotKeyword,
                SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker));

            finalPattern = SyntaxFactory.UnaryPattern(notToken, nullPattern);
        }

        // Ключевое слово 'is' с эластичными маркерами пробелов
        var isToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker),
            SyntaxKind.IsKeyword,
            SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker));

        // 3. Собираем финальное выражение 'expression is null'
        var newPatternExpression = SyntaxFactory.IsPatternExpression(expressionToCheck, isToken, finalPattern)
            .WithLeadingTrivia(leadingTrivia); // Возвращаем оригинальные отступы на место

        // 4. ДОБАВЛЯЕМ АННОТАЦИЮ ФОРМАТИРОВАНИЯ (Критический шаг из документации!)
        var formattedPattern = newPatternExpression.WithAdditionalAnnotations(Formatter.Annotation);

        // 5. Заменяем старый узел на новый и возвращаем документ (строго 3 шага из гайда)
        SyntaxNode oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode newRoot = oldRoot.ReplaceNode(binaryExpression, formattedPattern);

        return document.WithSyntaxRoot(newRoot);
    }
}
