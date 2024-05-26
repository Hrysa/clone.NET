using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Clone;

public static class Cloner
{
    private static readonly Dictionary<Type, MethodInfo> Cache = new();

    public static T Make<T>(T t) where T : new()
    {
        Type typ = t!.GetType();

        if (!Cache.TryGetValue(typ, out var method))
        {
            bool itf = typ.GetInterfaces().Any(x => x == typeof(IClone<T>));

            if (!itf)
            {
                throw new Exception("isn't cloneable object");
            }

            method = t.GetType().GetMethods().First(x => x.DeclaringType == typ && x.Name == "Clone");
            Cache[typ] = method;
        }

        T target = (T)Activator.CreateInstance(typ);
        method.Invoke(t, [target]);

        return target;
    }
}
