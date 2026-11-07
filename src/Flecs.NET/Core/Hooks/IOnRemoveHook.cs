namespace Flecs.NET.Core.Hooks;

/// <summary>
///     Interface for registering an on remove hook callback with the provided type.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public interface IOnRemoveHook<T> : IHookProvider where T : IOnRemoveHook<T>
{
    /// <summary>
    ///     The on remove hook callback.
    /// </summary>
    /// <param name="it">The iterator.</param>
    /// <param name="i">The entity row.</param>
    /// <param name="data">The reference to the component.</param>
    public static abstract void OnRemove(Iter it, int i, ref T data);

    unsafe void* IHookProvider.OnRemovePointer => (delegate*<Iter, int, ref T, void>)&T.OnRemove;
}
