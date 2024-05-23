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

    protected readonly string Indent;
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
        string clzName = _node.Identifier.NormalizeWhitespace().ToFullString();
        string modifers = string.Join(" ", _node.Modifiers.Select(x => x.NormalizeWhitespace().ToFullString()));

        _sb.AppendLine($"{Indent}{modifers} class {clzName}");
        _sb.AppendLine($"{Indent}{{");
        _sb.AppendLine($"{Indent}    public {clzName} Clone()");
        _sb.AppendLine($"{Indent}    {{");
        _sb.AppendLine($"{Indent}        var obj = new {clzName}();");
    }

    protected override void CreateEnd()
    {
        _sb.AppendLine($"{Indent}        return obj;");
        _sb.AppendLine($"{Indent}    }}");
        _sb.AppendLine($"{Indent}}}");
    }
}

class FieldBuilder : SyntaxBuilder
{
    private readonly FieldDeclarationSyntax _node;
    private readonly Compilation _compilation;

    public FieldBuilder(FieldDeclarationSyntax node, Compilation compilation, string indent) : base(indent)
    {
        _node = node;
        _compilation = compilation;
    }

    protected override void CreateBegin()
    {
        var model = _compilation.GetSemanticModel(_node.SyntaxTree);

        var variable = _node.Declaration.Variables.First();
        var fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;

        Console.WriteLine($"[new field] {fieldSymbol!.Type} {fieldSymbol.Type.SpecialType} {fieldSymbol.Type}");

        var t = GetTypedConstantKind(fieldSymbol.Type, _compilation);

        // not primitive
        if (t == TypedConstantKind.Error)
        {
            _sb.AppendLine($"{Indent}    throw new Exception(\"error type {fieldSymbol.Type} {fieldSymbol.Name}\");");
        }

        if (t == TypedConstantKind.Type)
        {
            Console.WriteLine(fieldSymbol.Type.OriginalDefinition.ToString());
            switch (fieldSymbol.Type.OriginalDefinition.ToString())
            {
                case "System.Collections.Generic.List<T>":
                {
                    _sb.AppendLine($"{Indent}    obj.{fieldSymbol.Name} = new ({fieldSymbol.Name}.Count);");
                    break;
                }
                default:
                {
                    _sb.AppendLine($"{Indent}    throw new Exception(\"unhandled field {fieldSymbol.Type} {fieldSymbol.Name}\");");
                    break;
                }
            }
            Console.WriteLine("Orgin " + fieldSymbol.Type.OriginalDefinition);

        }
        else if (t == TypedConstantKind.Array)
        {
            _sb.AppendLine(
                $"{Indent}    obj.{fieldSymbol.Name} = new {((IArrayTypeSymbol)fieldSymbol.Type).ElementType}[{fieldSymbol.Name}.Length];");
        }
        else
        {
            _sb.AppendLine($"{Indent}    obj.{fieldSymbol.Name} = {fieldSymbol.Name};");
        }


        //

        // Console.WriteLine(_node.Declaration.Variables);

        // _sb.AppendLine($"{Indent} private ");
    }

    // GenerateExpression()
    // {
    //     
    // }

    protected override void CreateEnd()
    {
    }

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