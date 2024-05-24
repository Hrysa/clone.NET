using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CloneGenerator;

public abstract class SyntaxBuilder
{
    protected SyntaxBuilder(string indent)
    {
        Indent = indent;
    }

    protected string Indent;
    protected StringBuilder _sb = new();
    protected List<SyntaxBuilder> _builders = new();

    protected abstract void CreateBegin();
    protected abstract void CreateEnd();

    public string Build()
    {
        CreateBegin();
        foreach (var builder in _builders)
        {
            _sb.Append(builder.Build());
        }

        CreateEnd();
        return _sb.ToString();
    }
}

public class NamespaceBuilder : SyntaxBuilder
{
    private string? _ns;

    public NamespaceBuilder(string? ns) : base(string.Empty)
    {
        _ns = ns;
    }


    public ClassBuilder CreateClass(ClassDeclarationSyntax node, Compilation compilation)
    {
        var builder = new ClassBuilder(node, compilation, _ns, _ns is null ? string.Empty : "    ");
        _builders.Add(builder);
        return builder;
    }

    protected override void CreateBegin()
    {
        _sb.AppendLine("using Clone;");
        _sb.AppendLine();

        if (_ns is null)
        {
            return;
        }

        _sb.AppendLine($"namespace {_ns}");
        _sb.AppendLine($"{Indent}{{");
    }

    protected override void CreateEnd()
    {
        if (_ns is null)
        {
            return;
        }

        _sb.AppendLine("}");
    }
}

public class ClassBuilder : SyntaxBuilder
{
    private readonly ClassDeclarationSyntax _node;
    private readonly Compilation _compilation;
    private readonly string? _ns;

    public ClassBuilder(ClassDeclarationSyntax node, Compilation compilation, string? ns, string indent) : base(indent)
    {
        _node = node;
        _compilation = compilation;
        _ns = ns;
    }


    public void CreateField(FieldDeclarationSyntax node, Compilation compilation)
    {
        _builders.Add(new FieldBuilder(node, compilation, Indent + "    "));
    }

    public void CreateProperty(PropertyDeclarationSyntax node)
    {
        // TODO
    }

    public string GetFullName()
    {
        string clazzName = _node.Identifier.NormalizeWhitespace().ToFullString();
        return _ns is null ? clazzName : $"{_ns}.{clazzName}";
    }

    protected override void CreateBegin()
    {
        var model = _compilation.GetSemanticModel(_node.SyntaxTree);
        var symbol = model.GetDeclaredSymbol(_node) as INamedTypeSymbol;

        string clzName = _node.Identifier.NormalizeWhitespace().ToFullString();
        string modifers = string.Join(" ", _node.Modifiers.Select(x => x.NormalizeWhitespace().ToFullString()));

        _sb.AppendLine($"{Indent}{modifers} class {clzName}: IClone<{clzName}>");
        _sb.AppendLine($"{Indent}{{");
        _sb.AppendLine($"{Indent}    public virtual void Clone({clzName} target)");
        _sb.AppendLine($"{Indent}    {{");

        if (symbol.BaseType is not null &&
            SyntaxHelper.GetTypedConstantKind(symbol.BaseType!, _compilation) == TypedConstantKind.Type)
        {
            _sb.AppendLine($"{Indent}        base.Clone(target);");
        }
    }

    protected override void CreateEnd()
    {
        _sb.AppendLine($"{Indent}    }}");
        _sb.AppendLine($"{Indent}}}");
    }
}

class FieldBuilder : SyntaxBuilder
{
    private readonly FieldDeclarationSyntax _node;
    private readonly Compilation _compilation;
    private int _version;
    private int _id;
    private static int _idStore;

    public FieldBuilder(FieldDeclarationSyntax node, Compilation compilation, string indent) : base(indent)
    {
        _node = node;
        _compilation = compilation;
        _id = ++_idStore;
    }

    protected override void CreateBegin()
    {
        var model = _compilation.GetSemanticModel(_node.SyntaxTree);

        var variable = _node.Declaration.Variables.First();
        var fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;

        _sb.AppendLine();
        GenerateExpression((INamedTypeSymbol)fieldSymbol!.Type, $"target.{fieldSymbol.Name}", fieldSymbol.Name, Indent);
    }

    private string GenerateExpression(INamedTypeSymbol type, string left, string right, string Indent)
    {
        TypedConstantKind kind = SyntaxHelper.GetTypedConstantKind(type, _compilation);
        string ver = $"_{_id}_{_version}";
        string rt = left;

        bool noLeft = left.Length == 0;
        if (left.Length == 0)
        {
            left = $"{type} r{ver}";
            rt = $"r{ver}";
        }

        switch (kind)
        {
            case TypedConstantKind.Error:
                _sb.AppendLine(
                    $"{Indent}    throw new Exception(\"error type {type} {right}\");");
                break;
            case TypedConstantKind.Type:
                if (type.IsGenericType)
                {
                    switch (type.ConstructUnboundGenericType().ToString())
                    {
                        case "System.Collections.Generic.List<>":
                        {
                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} r{{ver}} = new ({{right}}.Count);
                                             {{Indent}}    {{type}} src{{ver}} = {{right}};
                                             {{Indent}}    for(int i{{ver}} = 0; i{{ver}} < src{{ver}}.Count; i{{ver}}++)
                                             {{Indent}}    {
                                             """);
                            _version++;

                            var rvar = GenerateExpression((INamedTypeSymbol)type.TypeArguments.First(), string.Empty,
                                $"src{ver}[i{ver}]", Indent + "    ");
                            string lvar = noLeft ? $"r{ver}" : left;

                            _sb.AppendLine($$"""{{Indent}}        r{{ver}}.Add({{rvar}});""");
                            if (!noLeft)
                            {
                                _sb.AppendLine($$"""{{Indent}}        {{left}} = r{{ver}};""");

                            }
                            _sb.AppendLine($$"""
                                             {{Indent}}    }
                                             """);
                            break;
                        }
                        case "System.Collections.Generic.Dictionary<,>":
                        {
                            _sb.AppendLine($$"""{{Indent}}    {{type}} r{{ver}} = new ();""");

                            if (!noLeft)
                            {
                                _sb.AppendLine($$"""{{Indent}}    {{left}} = r{{ver}};""");
                            }

                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} src{{ver}} = {{right}};
                                             {{Indent}}    foreach(var kv{{ver}} in  src{{ver}})
                                             {{Indent}}    {
                                             """);
                            _version++;
                            var rtk = GenerateExpression((INamedTypeSymbol)type.TypeArguments.First(), string.Empty,
                                $"kv{ver}.Key", Indent + "    ");

                            _version++;
                            var rtv = GenerateExpression((INamedTypeSymbol)type.TypeArguments.Last(), string.Empty,
                                $"kv{ver}.Value", Indent + "    ");
                            string lk = noLeft ? $"r{ver}" : left;

                            _sb.AppendLine($$"""
                                             {{Indent}}        r{{ver}}.Add({{rtk}}, {{rtv}});
                                             {{Indent}}    }
                                             """);
                            break;
                        }
                        default:
                        {
                            InjectUnhandledThrow(type, right, Indent);
                            break;
                        }
                    }
                }
                else
                {
                    if (type.GetAttributes().Any(x => x.ToString() == "Clone.CloneableAttribute"))
                    {
                        _sb.AppendLine($"{Indent}    {left} =  Cloner.Make({right});");
                        // _sb.AppendLine($"{Indent}    {{");
                        // _sb.AppendLine($"{Indent}       var method = {right}.GetType().GetMethods().First(x => x.DeclaringType == {right}.GetType());");
                        // _sb.AppendLine($"{Indent}       method.Invoke({right}, [target]);");
                        // _sb.AppendLine($"{Indent}    }}");
                    }
                    else
                    {
                        InjectUnhandledThrow(type, right, Indent);
                    }
                }

                break;
            case TypedConstantKind.Array:
                _sb.AppendLine(
                    $"{Indent}    {left} = new {((IArrayTypeSymbol)type).ElementType}[{right}.Length];");
                break;
            case TypedConstantKind.Primitive:
                _sb.AppendLine($"{Indent}    {left} = {right};");

                break;
            default:
            {
                _sb.AppendLine($"{Indent}    // can not determine type {type} {right} ");
                break;
            }
        }

        return rt;
    }

    private void InjectUnhandledThrow(INamedTypeSymbol type, string def, string indent)
    {
        _sb.AppendLine($"{indent}    throw new Exception(\"unhandled field {type} {def}\");");
    }

    protected override void CreateEnd()
    {
    }
}

public static class SyntaxHelper
{
    internal static TypedConstantKind GetTypedConstantKind(ITypeSymbol type, Compilation compilation)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Byte:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Char:
            case SpecialType.System_String:
            case SpecialType.System_Object:
                return TypedConstantKind.Primitive;
            default:
                switch (type.TypeKind)
                {
                    case TypeKind.Array:
                        return TypedConstantKind.Array;
                    case TypeKind.Enum:
                        return TypedConstantKind.Enum;
                    case TypeKind.Error:
                        return TypedConstantKind.Error;
                }

                return TypedConstantKind.Type;
            // throw new Exception("wtf is this type");

            // default:
            //     switch (type.TypeKind)
            //     {
            //         case TypeKind.Array:
            //             return TypedConstantKind.Array;
            //         case TypeKind.Enum:
            //             return TypedConstantKind.Enum;
            //         case TypeKind.Error:
            //             return TypedConstantKind.Error;
            //     }
            //
            //     if (compilation != null &&
            //         compilation.IsSystemTypeReference(type))
            //     {
            //         return TypedConstantKind.Type;
            //     }
            //
            //     return TypedConstantKind.Error;
        }
    }
}
