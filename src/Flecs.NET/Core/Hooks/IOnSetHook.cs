namespace Flecs.NET.Core.Hooks;

/// <summary>
///     Interface for registering an on set hook callback with the provided type.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public interface IOnSetHook<T> : IHookProvider where T : IOnSetHook<T>
{
    /// <summary>
    ///     The on set hook callback.
    /// </summary>
    /// <param name="it">The iterator.</param>
    /// <param name="i">The entity row.</param>
    /// <param name="data">The reference to the component.</param>
    public static abstract void OnSet(Iter it, int i, ref T data);

    unsafe void* IHookProvider.OnSetPointer => (delegate*<Iter, int, ref T, void>)&T.OnSet;
}
