# Frequently Asked Questions

### Questions
* [Does UdonSharp Support X feature?](#does-udonsharp-support-x-feature)
* [Are prefabs supported fully?](#are-prefabs-supported-fully)
* [Can I access the player camera?](#can-i-access-the-player-camera)
* [Can I have more than one UdonSharp Udon Behavior on a GameObject?](#can-i-have-more-than-one-udonsharp-udon-behavior-on-a-gameobject)
* [I'm starting from scratch and need to use C# tutorials. What common aspects of C# don't work in UdonSharp?](#im-starting-from-scratch-and-need-to-use-c-tutorials-what-common-aspects-of-c-dont-work-in-udonsharp)
---

### Does UdonSharp support X feature?
If Udon supports it, then so does UdonSharp.

_Check [Class exposure tree](https://github.com/Merlin-san/UdonSharp/wiki/class-exposure-tree)_

### Are prefabs supported fully?
You can use prefabs with Udon and U#, but changes to serialized fields on those prefabs will not propagate to instances of the prefab correctly due to limitations in Unity.

### Can I access the player camera?
No, you can not access the player's camera. You can, however, get the head position and rotation.

See [VRCPlayerApi.GetTrackingData](https://github.com/Merlin-san/UdonSharp/wiki/vrchat-api#vrchatplayerapi)
 
`Vector3 headPos = localPlayer.GetTrackingData(TrackingData.Head).position`

### Can I have more than one UdonSharp Udon Behavior on a GameObject?
Yes.

### I'm starting from scratch and need to use C# tutorials. What common aspects of C# don't work in UdonSharp?
If you are learning UdonSharp and not familiar with C# already, you may run across some commonly used techniques that don't work in Udon and UdonSharp yet. These include, but are not limited to, the following:
- Enums not already defined by Unity
- Generic classes (`Class<T>`) and methods
- Inheritance
- Interfaces
- Method overloads
- Properties

The UdonSharp [readme](https://github.com/Merlin-san/UdonSharp/blob/master/README.md#c-features-supported) lists additional specific C# features that do not work.