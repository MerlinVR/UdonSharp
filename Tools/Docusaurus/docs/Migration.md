# Migration

UdonSharp 0.x (the .unitypackage version) is deprecated and no longer supported. This new version is easy to get through the [Creator Companion](https://vcc.docs.vrchat.com), which will help you keep it up-to-date as well.

[Migrating Projects using the Creator Companion](https://vcc.docs.vrchat.com/vpm/migrating).

## Known Issues

### Nested Prefabs

**Issue**: UdonSharp always warned against using nested prefabs, and now they will completely break in some circumstances.

**Symptoms**:

**How to Fix**:

### Does Not Belong to U# Assembly

**Issue**: Libraries with their own Assembly Definitions need to have an U# assembly definition, too.

**Symptoms**: An error like this: `[UdonSharp] Script 'Assets/MyScript.cs' does not belong to a U# assembly, have you made a U# assembly definition for the assembly the script is a part of?`

**How to Fix**:
1. Use the Project window to find the file ending in `.asmdef` in the same or a parent directory of the script in question. 
2. Right-click in the folder which has this Assembly Definition and choose `Create > U# Assembly Definition`. 
3. Select this new U# asmdef, and use the Inspector to set its "Source Assembly" to the other Assembly Definition File. 
4. You may need to restart Unity after doing this.

### Newtonsoft.Json.Dll

**Issue**: Some packages include their own copy of this JSON library, which the VRCSDK pulls in itself. This results in two copies of the library.

**Symptoms**: Errors in your console which mention the above library. It might not be at the front of the sentence, but something like `System.TypeInitializationException: the type initializer for blah blah blah...Assets/SketchfabForUnity/Dependencies/Libraries/Newtonsoft.Json.dll`

**How to Fix**: Remove any copies of Newtonsoft.Json.dll from your Assets folder, the VRCSDK will provide it for any package that needs it through the Package Manager.