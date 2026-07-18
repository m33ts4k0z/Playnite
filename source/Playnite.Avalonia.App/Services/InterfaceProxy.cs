using System.Reflection;

namespace Playnite.Avalonia.App.Services;

internal class InterfaceDispatchProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler { get; set; }

    protected override object Invoke(MethodInfo targetMethod, object[] args) =>
        Handler?.Invoke(targetMethod, args ?? Array.Empty<object>()) ??
        InterfaceProxy.DefaultValue(targetMethod.ReturnType);
}

internal static class InterfaceProxy
{
    public static T Create<T>(Func<MethodInfo, object[], object> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, InterfaceDispatchProxy>();
        ((InterfaceDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public static object DefaultValue(Type type)
    {
        if (type == typeof(void))
        {
            return null;
        }

        if (type == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var value = DefaultValue(type.GetGenericArguments()[0]);
            return typeof(Task).GetMethod(nameof(Task.FromResult))
                .MakeGenericMethod(type.GetGenericArguments()[0])
                .Invoke(null, new[] { value });
        }

        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType(), 0);
        }

        if (type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(List<>) ||
             type.GetGenericTypeDefinition() == typeof(IList<>) ||
             type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
        {
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]));
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
