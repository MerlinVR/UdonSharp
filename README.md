# UdonSharp
## An experimental compiler for compiling C#-like syntax to Udon assembly

UdonSharp is a compiler that compiles C#-like syntax to Udon assembly. UdonSharp is not currently conformant to any version of the C# language specification, so there are many things that are not implemented or will not work. If you want to learn C#, I don't recommend you use UdonSharp for learning as it is right now, since there may be language features tutorials assume exist that don't yet exist in U#. 

This compiler is in a very early state with only about two weeks of work on it so far. There has been very little work done on optimizations. Despite that, programs compiled by this generally perform similarly to their graph-compiled counterparts. Though due to how Udon currently handles copying structs, UdonSharp scripts can generate more garbage than the graph counterparts at the moment. 

## Features that Udon supports which are currently not supported by U#
- Marking variables as network synced
- That's all as far as I know

## C# features supported
- Automatic property and field accessor handling for getting and setting
- Flow control
  - Supports: `if` `else` `while` `for` `do` `foreach` `return` `break` `continue` `ternary operator (condition ? true : false)`
  - `switch` is currently not supported, but is planned
  - `goto`: https://xkcd.com/292/ I may add it in the future anyways
- Extern method overload resolution
- Implicit and explicit type conversions
- Arrays and array indexers
- All builtin arithmetic operators that Udon exposes (BitwiseNot is not implemented on Udon's side yet and I don't feel like making a special condition for it)
- Conditional short circuiting `(true || CheckIfTrue())` will not execute CheckIfTrue()
- `typeof()`
- Extern methods with out or ref parameters (such as many variants of `Physics.Raycast()`)
- User defined methods with parameters and return values. (This does not currently support method overloads, default parameter values, or `ref`/`params` parameters)

## Differences from regular Unity C# to note
- For the best experience making UdonSharp scripts, make your scripts inherit from `UdonSharpBehavior` instead of `MonoBehaviour`
- `Instantiate()` uses a method named `VRCInstantiate()` currently since VRC handles instantiate differently.
- The template variants of functions like `GetComponent<Transform>()` do not work currently, this is high priority. But you can use the type argument versions of them for now by calling `(Transform)GetComponent(typeof(Transform))`, it's just a little more verbose.
- Udon currently only supports array `[]` collections and by extension UdonSharp only supports arrays at the moment. It looks like they might support `List<T>` at some point, but it is not there yet. 
- User defined methods currently cannot be recursive. They will technically compile, but will likely break because all invocations of a function currently share the same "stack" variables. Support for this is planned as an optional attribute since implementing recursion with Udon's primitives makes it very performance heavy.
