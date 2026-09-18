using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Feeds.Providers;
using WinRT;

namespace MisWidgets.Feed.Provider.Com;

internal static class ComGuids
{
    public const string IClassFactory = "00000001-0000-0000-C000-000000000046";
    public const string IUnknown = "00000000-0000-0000-C000-000000000046";
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid(ComGuids.IClassFactory)]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(nint outerUnknown, ref Guid interfaceId, out nint instance);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool isLocked);
}

internal static class ClassObject
{
    private const uint ClsctxLocalServer = 0x4;
    private const uint RegclsMultipleUse = 0x1;

    public static void Register(Guid classId, object factory, out uint cookie)
    {
        int result = CoRegisterClassObject(
            classId,
            factory,
            ClsctxLocalServer,
            RegclsMultipleUse,
            out cookie);
        Marshal.ThrowExceptionForHR(result);
    }

    public static int Revoke(uint cookie) => CoRevokeClassObject(cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid classId,
        [MarshalAs(UnmanagedType.IUnknown)] object factory,
        uint classContext,
        uint flags,
        out uint cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint cookie);
}

internal sealed class FeedProviderFactory<T> : IClassFactory
    where T : IFeedProvider, new()
{
    private const int ClassENoAggregation = unchecked((int)0x80040110);
    private const int ENoInterface = unchecked((int)0x80004002);

    public int CreateInstance(nint outerUnknown, ref Guid interfaceId, out nint instance)
    {
        instance = nint.Zero;
        if (outerUnknown != nint.Zero)
        {
            return ClassENoAggregation;
        }

        if (interfaceId != typeof(T).GUID &&
            interfaceId != typeof(IFeedProvider).GUID &&
            interfaceId != Guid.Parse(ComGuids.IUnknown))
        {
            return ENoInterface;
        }

        instance = MarshalInspectable<IFeedProvider>.FromManaged(new T());
        return 0;
    }

    public int LockServer(bool isLocked) => 0;
}
