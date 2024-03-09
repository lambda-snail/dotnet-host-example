# Brief

A small repository to test out some scenarios related to writing a custom dotnet host in c++. There is an [official article](https://learn.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting)
about how to make a dotnet host using the `nethost` api, that comes with a [sample repository](https://github.com/dotnet/samples/tree/main/core/hosting) in github.

However, this project is very complicated and did not work well for me using Rider. Moreover it only contains examples of simple operations
such as loading the relevant dll's and calling a simple function.

This repository is an attempt to reduce the complexity of the example slightly, make it work with Rider and add more examples for my own reference.

# How to Run

All compilation and running is through the project called `NativeHost` that shows up as a c# project in Rider.

# Added Examples

## Hosted Code Calling Functions Exposed by the Host

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

Now we can either give the c# code a pointer to a function or member variable:

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

That's it, we now know how to expose functions for consumption by the hosted dotnet runtime. It was a bit surprising that the documentation doesn't say anything about this (at least as far as I can tell), but maybe it's just me who cannot read.


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

# Interesting Links

Other sites that could be of interest when researching this topic:

- https://github.com/KevinGliewe/embedded_dotnet_runtime_examples
- https://github.com/AaronRobinsonMSFT/DNNE/tree/master