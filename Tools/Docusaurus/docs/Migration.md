# Migration

UdonSharp 0.x (the .unitypackage version) is deprecated and no longer supported. This new version is easy to get through the [Creator Companion](https://vcc.docs.vrchat.com), which will help you keep it up-to-date as well.

[Migrating Projects using the Creator Companion](https://vcc.docs.vrchat.com/vpm/migrating).

## Known Issues

### Nested Prefabs

**Issue**: UdonSharp always warned against using nested prefabs, and now they will completely break in some circumstances.

**Symptoms**:

**How to Fix**:

### Newtonsoft.Json.Dll

**Issue**: Some packages include their own copy of this JSON library, which the VRCSDK pulls in itself. This results in two copies of the library.

**Symptoms**: Errors in your console which mention the above library. It might not be at the front of the sentence, but something like `System.TypeInitializationException: the type initializer for blah blah blah...Assets/SketchfabForUnity/Dependencies/Libraries/Newtonsoft.Json.dll`

**How to Fix**: Remove any copies of Newtonsoft.Json.dll from your Assets folder, the VRCSDK will provide it for any package that needs it through the Package Manager.