#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CustomMarshalling;

/// <summary>
/// Example of how to implement a custom marshaller for strings for use with source generation
/// Read more: https://learn.microsoft.com/en-us/dotnet/standard/native-interop/tutorial-custom-marshaller
/// https://github.com/dotnet/runtime/blob/main/docs/design/libraries/LibraryImportGenerator/UserTypeMarshallingV2.md
/// </summary>
[CustomMarshaller(typeof(string), MarshalMode.Default, typeof(CustomMarshallerWithLibraryImport))]
// [CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedIn, typeof(CustomMarshallerWithLibraryImport))]
// [CustomMarshaller(typeof(string), MarshalMode.UnmanagedToManagedOut, typeof(CustomMarshallerWithLibraryImport))]
internal static unsafe class CustomMarshallerWithLibraryImport
{
    public static UInt32* ConvertToUnmanaged(string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            throw new InvalidOperationException("Unable to marshal object");
        }

        return (UInt32*)(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Marshal.StringToCoTaskMemUni(str).ToPointer()
            : Marshal.StringToCoTaskMemUTF8(str).ToPointer());
    }

    public static string? ConvertToManaged(uint* pNativeData)
    {
        throw new NotImplementedException();
        // ArgumentNullException.ThrowIfNull(pNativeData);
        //
        // var ptr = new UIntPtr(pNativeData);
        //
        // return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        //     ? Marshal.PtrToStringUni(pNativeData)!
        //     : Marshal.PtrToStringUTF8(ptr)!;
    }

    public static void Free(UInt32* unmanaged) {}
}

/// <summary>
/// Example of how to implement a custom marshaller for strings.
/// Read more at: https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-runtime-interopservices-icustommarshaler
/// </summary>
public unsafe class CustomMarshallerExample : ICustomMarshaler
{
    public void CleanUpManagedData(object ManagedObj) { }

    public void CleanUpNativeData(IntPtr pNativeData) { }

    public int GetNativeDataSize()
    {
        throw new NotImplementedException();
    }

    public IntPtr MarshalManagedToNative(object ManagedObj)
    {
        if (ManagedObj is string str)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Marshal.StringToCoTaskMemUni(str)
                : Marshal.StringToCoTaskMemUTF8(str);
        }

        throw new InvalidOperationException("Unable to marshal object");
    }

    public object MarshalNativeToManaged(IntPtr pNativeData)
    {
        ArgumentNullException.ThrowIfNull(pNativeData.ToPointer());
        
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Marshal.PtrToStringUni(pNativeData)!
            : Marshal.PtrToStringUTF8(pNativeData)!;
    }
}