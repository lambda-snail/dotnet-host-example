using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using CustomMarshalling;

[module: System.Runtime.InteropServices.DefaultCharSet( CharSet.Unicode )]
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshallingAttribute] // Error SYSLIB1051, https://learn.microsoft.com/en-gb/dotnet/fundamentals/syslib-diagnostics/syslib1050-1069

namespace CustomMarshalling
{
}

namespace DotNetLib
{
    public static partial class Lib
    {
        static Lib()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetAssembly(typeof(Lib))!, (name, _, paths) =>
            {
                if (name == "__Internal")
                {
                    return NativeLibrary.GetMainProgramHandle(); // After dotnet 7, https://github.com/dotnet/runtime/issues/56331
                }
                return IntPtr.Zero;
            });
        }
        
        private static int s_CallCount = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct LibArgs
        {
            public IntPtr Message;
            public int Number;
        }

        public static int Hello(IntPtr arg, int argLength)
        {
            if (argLength < System.Runtime.InteropServices.Marshal.SizeOf(typeof(LibArgs)))
            {
                return 1;
            }

            LibArgs libArgs = Marshal.PtrToStructure<LibArgs>(arg);
            Console.WriteLine($"Hello, world! from {nameof(Lib)} [count: {s_CallCount++}]");
            PrintLibArgs(libArgs);
            return 0;
        }

        [UnmanagedCallersOnly]
        public static unsafe void TestFnPtr(delegate*<void> fn_from_cpp)
        {
            Console.WriteLine($"[C#] Preparing to call c++ function");
            fn_from_cpp();
        }
        
        [UnmanagedCallersOnly]
        public static unsafe void TestFnPtrWithArgs(delegate* unmanaged<int, double> fn_from_cpp)
        {
            Console.WriteLine($"[C#] Entering {nameof(TestFnPtrWithArgs)}");
            double ret = fn_from_cpp(20);
            Console.WriteLine($"[C#] C++ function returned {ret}!");
        }

        [UnmanagedCallersOnly]
        public static unsafe void TestStringInputOutput(delegate* unmanaged<IntPtr, IntPtr> str_fn)
        {
            Console.WriteLine($"[C#] Entering {nameof(TestStringInputOutput)}");

            string str_from_cs = "String from c#";
            IntPtr cpp_str = str_fn(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                        ? Marshal.StringToCoTaskMemUni(str_from_cs)
                                        : Marshal.StringToCoTaskMemUTF8(str_from_cs));
            
            
            
            string cs_str = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Marshal.PtrToStringUni(cpp_str)
                : Marshal.PtrToStringUTF8(cpp_str);
            
            Console.WriteLine($"[C#] String from c++: {cs_str}");
        }
        
        public delegate void CustomEntryPointDelegate(LibArgs libArgs);
        public static void CustomEntryPoint(LibArgs libArgs)
        {
            Console.WriteLine($"Hello, world! from {nameof(CustomEntryPoint)} in {nameof(Lib)}");
            PrintLibArgs(libArgs);
        }

        // P/Invoke functions
        // These demonstrate how to use the LibraryImport attribute to generate marshalling code
        // https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation
        // https://github.com/dotnet/runtime/tree/main/docs/design/libraries/LibraryImportGenerator
        
        // [DllImport("__Internal")] - before dotnet 7.0
        [LibraryImport("__Internal", EntryPoint = "print_simple_message")]
        private static partial void PrintSimpleMessage();
        
        [LibraryImport("__Internal", EntryPoint = "print_int")]
        private static partial void PrintInt(int i);
        
        [LibraryImport("__Internal", EntryPoint = "print_float")]
        private static partial void PrintFloat(float f);

        // Struct matching the one defined in c++
        [StructLayout(LayoutKind.Sequential)]
        struct ComplicatedParamStruct
        {
            public int SomeOption { get; set; }
            public double ValueOfOption { get; set; }
            public bool DoComplicatedThingy { get; set; }
        };
        
        [LibraryImport("__Internal", EntryPoint = "print_struct_pointer")]
        private static partial void PrintStructPointer(ref ComplicatedParamStruct @params);
        
        [LibraryImport("__Internal", EntryPoint = "print_struct_copy")]
        private static partial void PrintStructCopy(ComplicatedParamStruct @params);
        
        [LibraryImport("__Internal", EntryPoint = "print_struct_reference")]
        private static partial void PrintStructReference(ref ComplicatedParamStruct @params);
        
        // The C++ function takes a char so we use Utf8. We could also define a flag when on windows and
        // conditionally compile the attribute with different string marshalling for different OSes. There
        // is also the property StringMarshallingCustomType but I haven't figured out how to use it yet.
        [LibraryImport("__Internal", EntryPoint = "native_log", StringMarshalling = StringMarshalling.Utf8)]
        // Can also use the attribute [MarshalAs(...)] on parameters and return value
        private static partial void NativeLog(string message);

        // Does not seem to be working (?): StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(CustomMarshallerExample)
        [LibraryImport("__Internal", EntryPoint = "native_log_custom_marshalling", StringMarshallingCustomType = typeof(CustomMarshallerWithLibraryImport))]
        //[DllImport("__Internal", EntryPoint = "native_log_custom_marshalling")]
        private static partial void NativeLogWithCustomMarshalling(string message); // [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CustomMarshallerExample))]
        
        [UnmanagedCallersOnly]
        public static void CustomEntryPointUnmanagedCallersOnly(LibArgs libArgs)
        {
            Console.WriteLine($"Hello, world! from {nameof(CustomEntryPointUnmanagedCallersOnly)} in {nameof(Lib)}");
            PrintLibArgs(libArgs);

            PrintSimpleMessage();
            PrintSimpleMessage();
            PrintSimpleMessage();
            
            PrintInt(42);
            PrintFloat(3.14f);

            ComplicatedParamStruct @params = new() { SomeOption = 11, ValueOfOption = 43.67, DoComplicatedThingy = false };
            PrintStructPointer(ref @params);
            PrintStructCopy(@params);
            PrintStructReference(ref @params);

            NativeLog("This string is from c#");
            NativeLogWithCustomMarshalling("This string is from c# - with customized marshalling :D");
        }

        private static void PrintLibArgs(LibArgs libArgs)
        {
            string message = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Marshal.PtrToStringUni(libArgs.Message)
                : Marshal.PtrToStringUTF8(libArgs.Message);

            Console.WriteLine($"-- message: {message}");
            Console.WriteLine($"-- number: {libArgs.Number}");
        }
    }
}
