namespace Flecs.NET.Core.Hooks;

/// <summary>
///     Interface for registering a move hook callback with the provided type.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public interface IMoveHook<T> : IHookProvider where T : IMoveHook<T>
{
    /// <summary>
    ///     The move hook callback
    /// </summary>
    /// <param name="dst">The reference to the component's move destination.</param>
    /// <param name="src">The reference to the component's move source.</param>
    /// <param name="typeInfo">The component's type info.</param>
    public static abstract void Move(ref T dst, ref T src, TypeInfo typeInfo);

    unsafe void* IHookProvider.MovePointer => (delegate*<ref T, ref T, TypeInfo, void>)&T.Move;
}
