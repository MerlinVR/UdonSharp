# Migration

UdonSharp 0.x (the .unitypackage version) is deprecated and no longer supported. This new version is easy to get through the [Creator Companion](https://vcc.docs.vrchat.com), which will help you keep it up-to-date as well. We recommend you [Migrate your Projects using the Creator Companion](https://vcc.docs.vrchat.com/vpm/migrating). If you want to do the migration manually, read [Manual Migration](#manual-migration).

## New Features in UdonSharp 1.0
* **More C# features** in your UdonSharp programs:
	* `static` methods
	* Generic `static` methods
	* `params`, `out`, `ref`, and default parameters
	* Extension methods
	* Inheritance, virtual methods, and abstract classes
	* Partial classes
	* Enums
- **Multi-edit** multiple UdonSharp scripts in the Unity inspector
- **Prefab variants**, **instances**, and **nesting** are now fully supported
- **Editor scripting** has been overhauled and simplified
- **Compiler fixes** and **optimizations**
- **Fixed various bugs**, edge cases, and other rough edges

## Known Issues

### Nested Prefabs

**Issue**: UdonSharp always warned against using nested prefabs, and now they will completely break in some circumstances.

**Symptoms**: Errors like `Cannot upgrade scene behaviour 'SomethingOrOther' since its prefab must be upgraded`

**How to Fix**: Unpack the prefab in your 0.x UdonSharp project first. You can also open the "Udon Sharp" menu item and choose "Force Upgrade".

### Does Not Belong to U# Assembly

**Issue**: Libraries with their own Assembly Definitions need to have an U# assembly definition, too.

**Symptoms**: An error like this: `[UdonSharp] Script 'Assets/MyScript.cs' does not belong to a U# assembly, have you made a U# assembly definition for the assembly the script is a part of?`

**How to Fix**:
1. Use the Project window to find the file ending in `.asmdef` in the same or a parent directory of the script in question. 
2. Right-click in the folder which has this Assembly Definition and choose `Create > U# Assembly Definition`. 
3. Select this new U# asmdef, and use the inspector to set its "Source Assembly" to the other Assembly Definition File. 
4. You may need to restart Unity after doing this.

### Newtonsoft.Json.Dll

**Issue**: Some packages include their own copy of this JSON library, which the VRCSDK pulls in itself. This results in two copies of the library.

**Symptoms**: Errors in your console which mention the above library. It might not be at the front of the sentence, but something like `System.TypeInitializationException: the type initializer for blah blah blah...Assets/SketchfabForUnity/Dependencies/Libraries/Newtonsoft.Json.dll`

**How to Fix**: Remove any copies of Newtonsoft.Json.dll from your Assets folder. The VRCSDK will provide it for any package that needs it through the Package Manager.

### Other breaking changes
- Your U# behaviour name must match the .cs file name
- Duplicate program assets may not reference the same `.cs` file
- Program assets must point to a script and may not be empty
- Editor scripting is now different: Data is owned by a C# proxy of the UdonSharpBehaviour, and the corresponding UdonBehaviour is empty until runtime.
- Obsoleted overloads for station and player join events may no longer be used

## Manual Migration

Follow these steps to upgrade a project that uses a version of UdonSharp below 1.0 without using the Creator Companion:

1. Back up your project.
2. Delete the VRCSDK folder, Udon folder, UdonSharp folder, and Gizmos/UdonSharp folders from your project's "Assets" folder.
3. Download and install the [Unity Package versions of the World SDK](https://vrchat.com/download/sdk3-worlds).
4. Download and install the [Unity Package version of the UdonSharp SDK](https://github.com/vrchat-community/UdonSharp/releases/download/1.1.7/com.vrchat.udonsharp-1.1.7.unitypackage).
