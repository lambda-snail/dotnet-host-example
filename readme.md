# Brief

A small repository to test out some scenarios related to writing a custom dotnet host in c++. There is an [official article](https://learn.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting)
about how to make a dotnet host using the `nethost` api, that comes with a [sample repository](https://github.com/dotnet/samples/tree/main/core/hosting) in github.

However, the sample project did not contain an example of the scenario that I was interested in, and I had some trouble making it work with Rider.

This repository is an attempt to make the example work with Rider and add more examples for my own reference. I have also taken the liberty to attempt to address 
some of the warnings that clang-tidy gives.

# Documentation

I haven't found any real documentation on this topic, apart from the article on MS Learn and the sample repository. There is also this [design document](https://github.com/dotnet/runtime/blob/main/docs/design/features/native-hosting.md)
on the native hosting feature that seems to document the functions used in the sample.

# How to Run

All compilation and running is through the project called `NativeHost` that shows up as a c# project in Rider.

# Added Examples

## Native Host Exposing Functions to Managed Assembly

I was mainly interested in how to make the hosted c# code call back into the host. A reply on [this](https://github.com/dotnet/runtime/issues/41319) github issue
suggested that you can do this by passing in a function pointer, without using the `Marshal.GetDelegateForFunctionPointer` that shows up if you google the topic.

In short, this can be achieved by declaring a method like so in c#:

```csharp
[UnmanagedCallersOnly]
public static unsafe void TestFnPtrWithArgs(delegate* unmanaged<int, double> fn_from_cpp) { ... }
```

This will provide the c# code with a delegate that it can use normally like this:

```csharp
double @return = fn_from_cpp(20);
```

Which would then invoke some code that is defined in the host. The c# code can also store this delegate for later use.

To expose a function from the c++ side we would need to first define the callback type:

```c++
typedef void (CORECLR_DELEGATE_CALLTYPE *send_callback_to_dotnet_fn)(double_t(*fn)(int32_t));
send_callback_to_dotnet_fn callback;
```

We can then get a pointer to the function in c#:

```c++
typedef void (CORECLR_DELEGATE_CALLTYPE *send_callback_to_dotnet_fn)(double_t(*fn)(int32_t));
send_callback_to_dotnet_fn callback;

rc = load_assembly_and_get_function_pointer(
    dotnetlib_path.c_str(),
    dotnet_type,
    STR("TestFnPtrWithArgs"),
    UNMANAGEDCALLERSONLY_METHOD,
    nullptr,
    (void**)&callback);
```

Now we can either give the c# code a pointer to a free function:

```c++
callback(&test_fn_arumgents_and_returns);
```

or we can give it a lambda function:

```c++
auto fn = [](int32_t i) -> double_t
{
    std::cout << "[C++] A lambda recieved " << i << " from dotnet!" << std::endl;
    return static_cast<double_t>(i) + 3.14;
}; 
callback(fn);
```

That's it, we now know how to expose functions for consumption by the hosted dotnet runtime!

## Marshalling Strings

There ample is documentation on how to handle string types when doing a P/Invoke, however I couldn't find anything that mentions
the case of a native host calling functions directly or exposing functionality via function pointers like we are doing here. Luckily it 
seems the same principles apply in our case as well! 

The signature of a method in c# to send and receive a string would be the following:

```csharp
delegate* unmanaged<IntPtr, IntPtr> str_fn
```

And the entire method in the test application looks like this:

```csharp
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
```

First we prepare a string for sending to the native code by calling either `Marshal.StringToCoTaskMemUni` or `Marshal.StringToCoTaskMemUTF8`
depending on whether we are on Windows or not. We then send this string to the native host using hte provided callback.

In the next step we receive a string from the native host, which we convert to something that dotnet can understand, again depending on
which operating system we are on. Finally we write this string to output. In the console we should see something like this:

```
[C#] Entering TestStringInputOutput                           
[C++] C# sent the following string: String from c#            
[C#] String from c++: This string is from c++ :)  
```

We may need to think about the character set that we are using, which is described in more detail [here](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/charset).
To this end I added the following attribute to the c# code:

```csharp
[module: System.Runtime.InteropServices.DefaultCharSet( CharSet.Unicode )]
```

If the strings show up as empty when printing, we can play around with this and the types that we use on the c++ side as well.

An interesting thing that I noticed was that I could send (but not receive) "raw" strings to c++ using the following signature:

```csharp
delegate* unmanaged<string, IntPtr> str_fn
```

To properly receive this in c++ I had to accept it as a `char const*` instead of a `wchar_t const*`:

```cpp
callback( [](char const* str) -> wchar_t const*
{
    std::cout << "[C++] C# sent the following string: " << str << std::endl;
    return STR("This string is from c++ :)");
});
```

Not sure to what extent this would work however or what the best practices are here. Do we always use the `Marshal` class 
to prepare ou strings for interop, or are there some scenarios where it is OK to pass "raw" strings like this?

### Functions for String Marshalling

There are many methods available in the `Marshal` class, but in this example we have used the following ones.

| c# => c++          | c# <= c++                                    |
|--------------------|----------------------------------------------|
| `PtrToStringUni`   | `StringToCoTaskMemUni`, `StringToHGlobalUni` |
| `PtrToStringUTF8`  | `StringToCoTaskMemUTF8`                      |

Not quite sure what the difference between `StringToHGlobalUni` and `StringToCoTaskMemUni` is - both are listed as the inverse of 
`PtrToStringUni` in the [documentation](https://learn.microsoft.com/en-us/dotnet/api/System.Runtime.InteropServices.Marshal.PtrToStringUni?view=net-6.0)

# Limitations

This has only been tested on Windows and Rider so far. In the future I hope to get the time to see if it runs on Linux as well. I would also like to see if I 
can remove all the "solution"-related things and convert it to using CMake.

# Todo

- [ ] Add more of my own examples
- [ ] See if it is possible to convert to a CMake project
- [ ] Make sure this compiles and runs on Linux

# License and acknowledgements 

This project is licensed under the MIT license. It contains modified code from the official dotnet samples repository found here:
https://github.com/dotnet/samples/tree/main/core/hosting

# Further Reading

- https://github.com/dotnet/runtime/blob/main/docs/design/features/native-hosting.md
- https://github.com/KevinGliewe/embedded_dotnet_runtime_examples
- https://github.com/AaronRobinsonMSFT/DNNE/tree/master