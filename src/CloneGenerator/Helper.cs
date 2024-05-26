using Microsoft.CodeAnalysis;

namespace CloneGenerator;

public static class Helper
{
    public static IEnumerable<ISymbol> GetAllMembers(this INamedTypeSymbol symbol, bool withoutOverride = true)
    {
        // Iterate Parent -> Derived
        if (symbol.BaseType != null)
        {
            foreach (var item in GetAllMembers(symbol.BaseType))
            {
                // override item already iterated in parent type
                if (!withoutOverride || !item.IsOverride)
                {
                    yield return item;
                }
            }
        }

        foreach (var item in symbol.GetMembers())
        {
            if (!withoutOverride || !item.IsOverride)
            {
                yield return item;
            }
        }
    }
}
