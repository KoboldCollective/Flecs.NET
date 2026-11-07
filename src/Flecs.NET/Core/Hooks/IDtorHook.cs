namespace Flecs.NET.Core.Hooks;

/// <summary>
///     Interface for registering a dtor hook callback with the provided type.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public interface IDtorHook<T> : IHookProvider where T : IDtorHook<T>
{
    /// <summary>
    ///     The dtor hook callback.
    /// </summary>
    /// <param name="data">The reference to the component.</param>
    /// <param name="typeInfo">The component's type info.</param>
    public static abstract void Dtor(ref T data, TypeInfo typeInfo);

    unsafe void* IHookProvider.DtorPointer => (delegate*<ref T, TypeInfo, void>)&T.Dtor;
}
