# UdonSharp
## A compiler for compiling C# to Udon assembly

UdonSharp is a compiler that compiles C# to Udon assembly. UdonSharp is not currently conformant to any version of the C# language specification, so there are many things that are not implemented or will not work.

## Features that Udon supports which are currently not supported by U#
- UdonSharp is currently at feature parity with the Udon graph as far as I am aware. Please message me or make an issue if you find something that should be supported, but is not.

## C# features supported
- Flow control
  - Supports: `if` `else` `while` `for` `do` `foreach` ~~`switch`~~ `return` `break` `continue` `ternary operator (condition ? true : false)` `??`
- Implicit and explicit type conversions
- Arrays and array indexers
- All builtin arithmetic operators
- Conditional short circuiting `(true || CheckIfTrue())` will not execute CheckIfTrue()
- `typeof()`
- Extern methods with out or ref parameters (such as many variants of `Physics.Raycast()`)
- User defined methods with parameters and return values, supports out/ref, extension methods, and `params`
- ~~User defined properties~~ support in the next few days
- Static user methods
- UdonSharpBehaviour inheritence, virtual methods, etc. Will support interfaces on UdonSharpBehaviours in the next couple weeks.
- Unity/Udon event callbacks with arguments. For instance, registering a OnPlayerJoined event with a VRCPlayerApi argument is valid.
- String interpolation
- Field initializers
- Jagged arrays
- Referencing other custom classes, accessing fields, and calling methods on them
- ~~Recursive method calls are supported via the `[RecursiveMethod]` attribute~~ temporarily disabled for 1.0, will be added in the next week or so

#### See the [Project Board](https://github.com/MerlinVR/UdonSharp/projects/1) for what's planned to be added in the next couple months

## Differences from regular Unity C# to note
- For the best experience making UdonSharp scripts, make your scripts inherit from `UdonSharpBehaviour` instead of `MonoBehaviour`
- If you need to call `GetComponent<UdonBehaviour>()` you will need to use `(UdonBehaviour)GetComponent(typeof(UdonBehaviour))` at the moment since the generic get component is not exposed for UdonBehaviour yet. GetComponent<T>() works for other Unity component types though.
- Udon currently only supports array `[]` collections and by extension UdonSharp only supports arrays at the moment. It looks like they might support `List<T>` at some point, but it is not there yet. 
- Use the `UdonSynced` attribute on fields that you want to sync.  
- Numeric casts are checked for overflow due to UdonVM limitations
- The internal type of variables returned by `.GetType()` will not always match what you may expect since U# abstracts some types in order to make them work in Udon. For instance, any jagged array type will return a type of `object[]` instead of something like `int[][]` for a 2D int jagged array.

## Udon bugs that affect U#
- Mutating methods on structs do not modify the struct (this can be seen on things like calling Normalize() on a Vector3) https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/raysetorigin-and-raysetdirection-not-working
- Instantiated objects will lose their UdonBehaviours when instantiated from a prefab and cannot be interacted with/triggered https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/interactive-objects-break-after-being-clonedinstanciated-on-live-worlds

## Setup

### Requirements
- Unity 2019.4.29f1
- [VRCSDK3 + UdonSDK](https://vrchat.com/home/download)
- The latest [release](https://github.com/Merlin-san/UdonSharp/releases/latest) of UdonSharp

### Installation
1. Read the getting started with Udon doc page https://docs.vrchat.com/docs/getting-started-with-udon this has basic installation instructions for Udon.
2. Install the latest version of the VRCSDK3 linked on the getting started.
3. Get the latest release of UdonSharp from [here](https://github.com/Merlin-san/UdonSharp/releases/latest) and install it to your project.

### Getting started
1. Make a new object in your scene
2. Add an `Udon Behaviour` component to your object
3. Below the "New Program" button click the dropdown and select "Udon C# Program Asset"
4. Now click the New Program button, this will create a new UdonSharp program asset for you
5. Click the Create Script button and choose a save destination and name for the script.
6. This will create a template script that's ready for you to start working on, open the script in your editor of choice and start programming

#### Asset explorer asset creation

Instead of creating assets from an UdonBehaviour you can also do the following:
1. Right-click in your project asset explorer
2. Navigate to Create > U# script
3. Click U# script, this will open a create file dialog
4. Choose a name for your script and click Save
5. This will create a .cs script file and an UdonSharp program asset that's set up for the script in the same directory

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

#### Other examples

For more example scripts take a look at the wiki page for [examples](https://github.com/Merlin-san/UdonSharp/wiki/examples), the Examples folder included with U#, or the [community resources](https://github.com/Merlin-san/UdonSharp/wiki/community-resources) page on the wiki.

## Credits
[**Toocanzs**](https://github.com/Toocanzs) - Implementing field initializers and helping with miscellaneous things

[**PhaxeNor**](https://github.com/PhaxeNor) - Help with wiki and documentation

[**bd_**](https://github.com/bdunderscore) - Significant optimizations to compiled code
  
[**mika-f**](https://github.com/mika-f/) - Implementation of user defined property support

[**UdonPie Compiler**](https://github.com/zz-roba/UdonPieCompiler) - For demonstrating how straightforward it can be to write a compiler for Udon

## Links
 [![Discord](https://img.shields.io/badge/Discord-My%20Discord%20Server-blueviolet?logo=discord)](https://discord.gg/Ub2n8ZA) - For support and bug reports
 
 <a href="https://www.patreon.com/MerlinVR"><img src="https://img.shields.io/endpoint.svg?url=https%3A%2F%2Fmerlin-patreon.herokuapp.com%2FMerlinVR" alt="Patreon donate button" /> </a> -  Support the development of UdonSharp
