using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Clone;

public static class Cloner
{
    private static readonly Dictionary<Type, Func<object, object>> Cache = new();

    public static T Make<T>(T t) where T : new()
    {
        if (t == null)
        {
            return default!;
        }

        Type typ = t.GetType();

        if (!Cache.TryGetValue(typ, out var method))
        {
            bool itf = typ.GetInterfaces().Any(x => x == typeof(IClone<T>));
            var methodInfo = typ.GetMethods().FirstOrDefault(x => x.DeclaringType == typ && x.Name == "Clone");

            if (!itf || methodInfo is null)
            {
                throw new Exception($"{typ} isn't cloneable object");
            }

            var param = Expression.Parameter(typeof(object));
            var target = Expression.Variable(typ, "target");
            var block = Expression.Block([target],
                Expression.Assign(target, Expression.New(typ)),
                Expression.Call(Expression.Convert(param, typ), methodInfo, Expression.Convert(target, typ)),
                target
            );
            method = Expression.Lambda<Func<object, object>>(block, param).Compile();

            Cache[typ] = method;
        }

        return (T)method(t);
    }
}
