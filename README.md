# UdonSharp
## An experimental compiler for compiling C#-like syntax to Udon assembly

UdonSharp is a compiler that compiles C#-like syntax to Udon assembly. UdonSharp is not currently conformant to any version of the C# language specification, so there are many things that are not implemented or will not work. If you want to learn C#, I don't recommend you use UdonSharp for learning as it is right now, since there may be language features tutorials assume exist that don't yet exist in U#. 

This compiler is in a very early state with only about two weeks of work on it so far. There has been very little work done on optimizations. Despite that, programs compiled by this generally perform similarly to their graph-compiled counterparts. Though due to how Udon currently handles copying structs, UdonSharp scripts can generate more garbage than the graph counterparts at the moment. 

## Features that Udon supports which are currently not supported by U#
- UdonSharp is currently at feature parity with the Udon graph as far as I am aware. Please message me or make an issue if you find something that should be supported, but is not.

## C# features supported
- Automatic property and field accessor handling for getting and setting
- Flow control
  - Supports: `if` `else` `while` `for` `do` `foreach` `return` `break` `continue` `ternary operator (condition ? true : false)` `??`
  - `switch` is currently not supported, but is planned
  - `goto` is not currently supported: https://xkcd.com/292/ I may add it in the future anyways
- Extern method overload resolution with support for default arguments and `params` argument lists
- Implicit and explicit type conversions
- Arrays and array indexers
- All builtin arithmetic operators that Udon exposes (BitwiseNot is not implemented on Udon's side yet and I don't feel like making a special condition for it)
- Conditional short circuiting `(true || CheckIfTrue())` will not execute CheckIfTrue()
- `typeof()`
- Extern methods with out or ref parameters (such as many variants of `Physics.Raycast()`)
- User defined methods with parameters and return values. (This does not currently support method overloads, default parameter values, or `ref`/`params` parameters)
- Unity/Udon event callbacks with arguments. For instance, registering a OnPlayerJoined event with a VRCPlayerApi argument is valid.
- String interpolation
- Field initilizers

## Differences from regular Unity C# to note
- For the best experience making UdonSharp scripts, make your scripts inherit from `UdonSharpBehaviour` instead of `MonoBehaviour`
- `Instantiate()` uses a method named `VRCInstantiate()` currently since VRC handles instantiate differently.
- The template variants of functions like `GetComponent<Transform>()` do not work currently, this is high priority. But you can use the type argument versions of them for now by calling `(Transform)GetComponent(typeof(Transform))`, it's just a little more verbose.
- Udon currently only supports array `[]` collections and by extension UdonSharp only supports arrays at the moment. It looks like they might support `List<T>` at some point, but it is not there yet. 
- User defined methods currently cannot be recursive. They will technically compile, but will likely break because all invocations of a function currently share the same "stack" variables. Support for this is planned as an optional attribute since implementing recursion with Udon's primitives makes it very performance heavy.
- Field initilizers are evaluated at compile time, if you have any init logic that depends on other objects in the scene you should use Awake or Start for this.
- Use the `UdonSynced` attribute on fields that you want to sync.  

## Setup

### Requirements
- Unity 2018.4.15 or greater
- VRCSDK3
- UdonSDK
- The latest [release](https://github.com/Merlin-san/UdonSharp/releases/latest) of UdonSharp

### Installation
1. Read the getting started with Udon official thread https://ask.vrchat.com/t/getting-started-with-udon/80 this has basic installation instructions for Udon.
2. Install the latest version of the VRCSDK3 and UdonSDK linked on the getting started thread. Make sure to install VRCSDK3 first.
3. Get the latest release of UdonSharp from [here](https://github.com/Merlin-san/UdonSharp/releases/latest) and install it to your project.

### Getting started
1. Make a new object in your scene
2. Add an `Udon Behaviour` component to your object
3. Below the "New Program" button click the dropdown and select "Udon C# Program Asset"
4. Now click the New Program button, this will create a new UdonSharp program asset for you
5. Click the Create Script button and choose a save destination and name for the script.
6. This will create a template script that's ready for you to start working on, open the script in your editor of choice and start programming

### Example scripts

#### The rotating cube demo

This rotates the object that it's attached to by 90 degrees every second

```cs
using UnityEngine;
using UdonSharp;

public class RotatingCubeBehaviour : UdonSharpBehaviour
{
    private void Update()
    {
        transform.Rotate(Vector3.up, 90f * Time.deltaTime);
    }
}
```

## Credits
[**Toocanzs**](https://github.com/Toocanzs) - Implementing field initializers and helping with miscellaneous things

[**UdonPie Compiler**](https://github.com/zz-roba/UdonPieCompiler) - For demonstrating how straightforward it can be to write a compiler for Udon

## Links
 [![Discord](https://img.shields.io/badge/Discord-My%20Discord%20Server-blueviolet?logo=discord)](https://discord.gg/Ub2n8ZA) - For support and bug reports
 
 [![Trello](https://img.shields.io/badge/Trello-Udon%20Sharp%20Trello-blueviolet?logo=trello)](https://trello.com/b/EkIGQBy2/udonsharp) - Look at what's planned and in progress
