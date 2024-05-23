using Microsoft.CodeAnalysis;
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

            var typeSymbol = semanticModel.GetDeclaredSymbol(clazz);
            if (typeSymbol == null)
            {
                return;
            }

            string? ns = typeSymbol.ContainingNamespace.Name;

            if (string.IsNullOrEmpty(ns))
            {
                ns = null;
            }

            // string? ns = GetNamespace(clazz)?.Name.NormalizeWhitespace().ToFullString();
            var namespaceBuilder = new NamespaceBuilder(ns);
            var classBuilder = namespaceBuilder.CreateClass(clazz, compilation);

            foreach (var memberDeclarationSyntax in clazz.Members)
            {
                switch (memberDeclarationSyntax)
                {
                    case FieldDeclarationSyntax field:
                    {
                        classBuilder.CreateField(field, compilation);
                        break;
                    }
                    case PropertyDeclarationSyntax prop:
                    {
                        classBuilder.CreateProperty(prop);
                        break;
                    }
                }
            }


            ctx.AddSource(classBuilder.GetFullName(), namespaceBuilder.Build());
        });
    }
}