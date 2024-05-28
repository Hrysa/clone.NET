﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CloneGenerator;

[Generator]
public class CloneIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var cloneableClassProvider = context.SyntaxProvider.ForAttributeWithMetadataName("Clone.CloneableAttribute",
            (node, _) => node is ClassDeclarationSyntax,
            (ctx, _) => ctx.TargetNode as ClassDeclarationSyntax);

        context.RegisterSourceOutput(cloneableClassProvider.Combine(context.CompilationProvider), (ctx, source) =>
        {
            var clazz = source.Left!;
            var compilation = source.Right!;

            if (!clazz.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword)))
            {
                // throw new Exception("class must be partial");
                // TODO FIXME show this error
                return;
            }

            var semanticModel = compilation.GetSemanticModel(clazz.SyntaxTree);

            var clazzSymbol = semanticModel.GetDeclaredSymbol(clazz);
            if (clazzSymbol == null)
            {
                return;
            }

            string? ns = clazzSymbol.ContainingNamespace.Name;

            if (string.IsNullOrEmpty(ns))
            {
                ns = null;
            }

            var namespaceBuilder = new NamespaceBuilder(ns);
            var classBuilder = namespaceBuilder.CreateClass(clazzSymbol, compilation);

            var members = clazzSymbol.GetMembers()
                .Where(x => x is (IFieldSymbol or IPropertySymbol) and
                    { IsStatic: false, IsImplicitlyDeclared: false, CanBeReferencedByName: true });

            foreach (var memberSymbol in members)
            {
                switch (memberSymbol.Kind)
                {
                    case SymbolKind.Field:
                    {
                        bool ignore = memberSymbol.GetAttributes()
                            .Any(x => x.ToString() is "Clone.CloneIgnoreAttribute");
                        if (ignore)
                        {
                            break;
                        }

                        classBuilder.CreateField(memberSymbol, compilation);

                        break;
                    }
                    case SymbolKind.Property:
                    {
                        var hasBody = false;
                        foreach (var reference in memberSymbol.DeclaringSyntaxReferences)
                        {
                            if (reference.GetSyntax() is PropertyDeclarationSyntax { ExpressionBody: not null })
                            {
                                hasBody = true;
                                break;
                            }
                        }

                        if (hasBody)
                            break;

                        classBuilder.CreateField(memberSymbol, compilation);
                        break;
                    }
                }
            }

            ctx.AddSource(string.Join(".", $"{clazzSymbol}.g.cs"), namespaceBuilder.Build());
        });
    }
}
