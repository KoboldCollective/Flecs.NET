namespace Flecs.NET.Core.Hooks;

/// <summary>
///     Interface for registering a copy hook callback with the provided type.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public interface ICopyHook<T> : IHookProvider where T : ICopyHook<T>
{
    /// <summary>
    ///     The copy hook callback
    /// </summary>
    /// <param name="dst">The reference to the component's copy destination.</param>
    /// <param name="src">The reference to the component's copy source.</param>
    /// <param name="typeInfo">The component's type info.</param>
    public static abstract void Copy(ref T dst, ref T src, TypeInfo typeInfo);

    unsafe void* IHookProvider.CopyPointer => (delegate*<ref T, ref T, TypeInfo, void>)&T.Copy;
}
