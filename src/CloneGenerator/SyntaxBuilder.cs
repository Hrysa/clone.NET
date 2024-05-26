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
        var childSb = new StringBuilder();

        try
        {
            foreach (var builder in _builders)
            {
                childSb.Append(builder.Build());
            }
        }
        catch (UnhandledCloneTypeException ex)
        {
            childSb.AppendLine(SymbolDisplay.FormatLiteral(ex.Message, false));
        }

        CreateBegin();
        _sb.Append(childSb);
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


    public ClassBuilder CreateClass(INamedTypeSymbol symbol, Compilation compilation)
    {
        var builder = new ClassBuilder(symbol, compilation, _ns, _ns is null ? string.Empty : "    ");
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
    private readonly INamedTypeSymbol _symbol;
    private readonly Compilation _compilation;
    private readonly string? _ns;

    public ClassBuilder(INamedTypeSymbol symbol, Compilation compilation, string? ns, string indent) : base(indent)
    {
        _symbol = symbol;
        _compilation = compilation;
        _ns = ns;
    }


    public void CreateField(IFieldSymbol syntax, Compilation compilation)
    {
        _builders.Add(new FieldBuilder(syntax, compilation, Indent + "    "));
    }

    public void CreateProperty(IPropertySymbol symbol)
    {
        // TODO
    }

    public string GetFullName()
    {
        return _symbol.Name;
    }

    protected override void CreateBegin()
    {
        string clazzName = _symbol.Name;

        _sb.AppendLine($"{Indent}partial class {clazzName}: IClone<{clazzName}>");
        _sb.AppendLine($"{Indent}{{");
        _sb.AppendLine($"{Indent}    public virtual void Clone({clazzName} target)");
        _sb.AppendLine($"{Indent}    {{");

        if (_symbol.BaseType is not null &&
            _symbol.BaseType.GetAttributes().Any(x => x.ToString() is "Clone.CloneIgnoreAttribute"))
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
    private readonly IFieldSymbol _symbol;
    private readonly Compilation _compilation;
    private int _version;
    private int _id;
    private static int _idStore;

    public FieldBuilder(IFieldSymbol symbol, Compilation compilation, string indent) : base(indent)
    {
        _symbol = symbol;
        _compilation = compilation;
        _id = ++_idStore;
    }

    protected override void CreateBegin()
    {
        _sb.AppendLine();
        GenerateExpression((INamedTypeSymbol)_symbol!.Type, $"target.{_symbol.Name}", _symbol.Name, Indent);
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
                            var argSymbol = (INamedTypeSymbol)type.TypeArguments.First();
                            var parentVar = noLeft ? "null" : left;
                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} r{{ver}} = {{parentVar}};
                                             {{Indent}}    if (r{{ver}} is null) r{{ver}} = new ({{right}}.Count);
                                             {{Indent}}    {{type}} src{{ver}} = {{right}};
                                             {{Indent}}    foreach({{argSymbol}} itm{{ver}} in src{{ver}})
                                             {{Indent}}    {
                                             """);
                            _version++;

                            var rvar = GenerateExpression(argSymbol, string.Empty,
                                $"itm{ver}", Indent + "    ");

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
                            var parentVar = noLeft ? "null" : left;
                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} r{{ver}} = {{parentVar}};
                                             {{Indent}}    if (r{{ver}} is null) r{{ver}} = new ();
                                             """);

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

                            _sb.AppendLine($$"""
                                             {{Indent}}        r{{ver}}.Add({{rtk}}, {{rtv}});
                                             {{Indent}}    }
                                             """);
                            break;
                        }
                        case "System.Collections.Generic.HashSet<>":
                        {
                            var parentVar = noLeft ? "null" : left;
                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} r{{ver}} = {{parentVar}};
                                             {{Indent}}    if (r{{ver}} is null) r{{ver}} = new ();
                                             """);

                            if (!noLeft)
                            {
                                _sb.AppendLine($$"""{{Indent}}    {{left}} = r{{ver}};""");
                            }

                            _sb.AppendLine($$"""
                                             {{Indent}}    {{type}} src{{ver}} = {{right}};
                                             {{Indent}}    foreach(var itm{{ver}} in  src{{ver}})
                                             {{Indent}}    {
                                             """);
                            _version++;
                            var rtv = GenerateExpression((INamedTypeSymbol)type.TypeArguments.Last(), string.Empty,
                                $"itm{ver}", Indent + "    ");

                            _sb.AppendLine($$"""
                                             {{Indent}}        r{{ver}}.Add({{rtv}});
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
        var location = _symbol.Locations.First().GetLineSpan();

        throw new UnhandledCloneTypeException(
            $"{indent}    throw new Exception(\"unhandled field {_symbol} in {location.Path}:line {location.StartLinePosition.Line}\");");
    }

    protected override void CreateEnd()
    {
    }
}

public class UnhandledCloneTypeException : Exception
{
    internal UnhandledCloneTypeException(string s) : base(s)
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
