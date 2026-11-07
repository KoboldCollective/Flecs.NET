using System.ComponentModel;

namespace Flecs.NET.Core.Hooks;

/// <summary>
///     Provides the function pointers of the type hooks implemented by a type. Hook interfaces override the
///     pointer of the hook they implement and leave the rest null. This avoids reflection, which is not
///     available when publishing with NativeAOT.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe interface IHookProvider
{
    internal void* CtorPointer => null;
    internal void* DtorPointer => null;
    internal void* CopyPointer => null;
    internal void* MovePointer => null;
    internal void* OnAddPointer => null;
    internal void* OnRemovePointer => null;
    internal void* OnSetPointer => null;
}
