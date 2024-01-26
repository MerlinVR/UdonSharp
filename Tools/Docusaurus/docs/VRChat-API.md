# VRChat API

## API
### Methods
* [VRCInstantiate](#vrcinstantiate)

### Classes
* [Utilities](#utilities)
* [VRCStation](#vrcstation)
* [Networking](#networking)
* [TrackingData](#trackingdata)
* [UdonBehaviour](#udonbehaviour)
* [VRCPlayerApi](#vrcplayerapi)
* [InputManager](#inputmanager)
* [SerializationResult](#inputmanager)
* [UdonInputEventArgs](#udoninputeventargs)
* [VRCUrl](#vrcurl)
* [VRCUrlInputField](#vrcurlinputfield)
* [VRCMirrorReflection](#vrcmirrorreflection)
* [VRCObjectPool](#vrcobjectpool)
* [VRCObjectSync](#vrcobjectsync)
* [VRCAvatarPedestal](#vrcavatarpedestal)
* [VRCPickup](#vrcpickup)
* [VRCPortalMarker](#vrcportalmarker)

### Enums
* [EventTiming](#eventtiming)
* [Mobility](#mobility)
* [NetworkEventTarget](#networkeventtarget)
* [SpawnOrientation](#spawnorientation)
* [TrackingDataType](#trackingdatatype)
* [VRCInputMethod](#vrcinputmethod)
* [HandType](#handtype)
* [UdonInputEventType](#udoninputeventtype)
* [VideoError](#videoerror)
* [AutoHoldMode](#autoholdmode)
* [PickupOrientation](#pickuporientation)
* [PickupHand](#pickuphand)

## Supported Features
* [Synced Variables](#synced-variables)

---

## Methods

### VRCInstantiate
| Static | Returns | Name | Summary |
| :---: | --- | --- | --- |
| ✔️ | [GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) | VRCInstantiate([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) original) | Creates a local, non-synced copy of an object. See [here](https://docs.unity3d.com/ScriptReference/Object.Instantiate.html) for more information. |


## Classes

### Utilities
`static class VRC.SDKBase.Utilities`

#### Methods
| Static | Returns | Name | Summary |
| :---: | --- | --- | --- |
| ✔️ | bool | IsValid(object obj) | Returns true if the specified object is valid and not a null reference, otherwise false.  This is typically used to check [VRCPlayerApi](#vrcplayerapi) objects after a player has left the instance, or [GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) objects that have been destroyed. |
| ✔️ | void | ShuffleArray(int[] array) | Randomly shuffles each element in the array. |

### VRCStation
`class VRC.SDK3.Components.VRCStation` / `class VRC.SDKBase.VRCStation`

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| [Mobility](#mobility) | PlayerMobility | Determines if the player be able to move.  Default value is `VRCStation.Mobility.Immobilize`. |
| bool | canUseStationFromStation | Determines if the user can switch stations when sitting in a station. Default value is `true`. |
| [RuntimeAnimatorController](https://docs.unity3d.com/ScriptReference/RuntimeAnimatorController.html) | animatorController | Used to override normal seating animations with a custom one. |
| bool | disableStationExit | If the user cannot exit the station by usual means, use triggers to unseat the user |
| bool | seated | Is this a station that the user should be sitting in? Default value is `true`. See [here](https://docs.vrchat.com/docs/vrc_station) for more information. |
| [Transform](https://docs.unity3d.com/ScriptReference/Transform.html) | stationEnterPlayerLocation | Transform used to define where the user should be transported to when seated |
| [Transform](https://docs.unity3d.com/ScriptReference/Transform.html) | stationExitPlayerLocation | Transform used to define where the user should be transported to when they are unseated |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| void | UseStation([VRCPlayerApi](#vrcplayerapi) player) | Uses the station |
| void | ExitStation([VRCPlayerApi](#vrcplayerapi) player) | Exits the station |

### Networking
`static class VRC.SDKBase.Networking`

#### Properties
| Static | Type | Name | Summary |
| :---: | --- | --- | --- |
| ✔️ | bool | IsMaster | Returns if the local player is the instance master |
| ✔️ | [VRCPlayerApi](#vrcplayerapi) | LocalPlayer | Returns the current player |
| ✔️ | bool | IsNetworkSettled | Returns true if the network is ready |

#### Methods
| Static | Returns | Name | Summary |
| :---: | --- | --- | --- |
| ✔️ | bool | IsOwner([VRCPlayerApi](#vrcplayerapi) player, [GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Returns if the given player is the owner over the object |
| ✔️ | bool | IsOwner([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Returns if the local player is the owner of the object |
| ✔️ | [VRCPlayerApi](#vrcplayerapi) | GetOwner([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Returns the owner of the given object |
| ✔️ | void | SetOwner([VRCPlayerApi](#vrcplayerapi) player, [GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Sets the provided player as the owner of the object |
| ✔️ | bool | IsObjectReady([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Returns if the object is ready |
| ✔️ | void | Destroy([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Destroys the given object |
| ✔️ | string | GetUniqueName([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | |
| ✔️ | [DateTime](https://docs.microsoft.com/dotnet/api/system.datetime) | GetNetworkDateTime() | |
| ✔️ | double | GetServerTimeInSeconds() | Returns the current server time in seconds. |
| ✔️ | int | GetServerTimeInMilliseconds() | Returns the current server time in milliseconds. |
| ✔️ | double | CalculateServerDeltaTime(double timeInSeconds, double previousTimeInSeconds) | Calculates the difference between two server time stamps as returned by `GetServerTimeInSeconds()`. |

### TrackingData
`struct VRC.SDKBase.VRCPlayerApi.TrackingData`

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| [Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) | position | The position of the player's tracking point |
| [Quaternion](https://docs.unity3d.com/ScriptReference/Quaternion.html) | rotation | The rotation of the player's tracking point |

### UdonBehaviour
`class VRC.Udon.UdonBehaviour`

A UdonBehaviour can be fetched with GetComponent.<br/>
Currently *does not* support `GetComponent<T>()`
```cs
UdonBehaviour behaviour = (UdonBehaviour)GetComponent(typeof(UdonBehaviour));
```

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| bool | DisableInteractive | Determines whether an object with an Interact event should accept pointer raycasts and show an interactable outline and tooltips. |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| void | SendCustomEvent(string eventName) | Runs a public method on the behaviour |
| void | SendCustomNetworkEvent([NetworkEventTarget](#networkeventtarget) target, string eventName) | Runs a public method over the network |
| void | SendCustomEventDelayedSeconds(string eventName, float delaySeconds, [EventTiming](#eventtiming) eventTiming) | Executes a custom event on the behaviour after a time delay, measured in seconds. |
| void | SendCustomEventDelayedFrames(string eventName, int delayFrames, [EventTiming](#eventtiming) eventTiming) | Executes a custom event on the behaviour after a frame delay. |
| object | GetProgramVariable(string symbolName) | Get a variable from the behaviour |
| void | SetProgramVariable(string symbolName, object value) | Sets a variable on the behaviour |
| [Type](https://docs.microsoft.com/dotnet/api/system.type) | GetProgramVariableType(string symbolName) | Retrieves the type of the specified variable from the behaviour. |
| void | RequestSerialization() | Triggers the serialization and transmission of any synced variable data to remote clients.  This is typically used when a behaviour is set to manual syncing mode. |

### VRCPlayerApi
`class VRC.SDKBase.VRCPlayerApi`

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| bool | isLocal | Returns if the given player is the local or remote |
| string | displayName | Returns the players display name |
| bool | isMaster | Returns if the player is the instance Master |
| int | playerId | Returns the players instance id |

#### Methods
| Static | Returns | Name | Summary |
| :---: | --- | --- | --- |
| | bool | IsPlayerGrounded() | Returns if the player is on the ground or not |
| ✔️ | int | GetPlayerId([VRCPlayerApi](#vrcplayerapi) player) | Returns the players instance id |
| ✔️ | [VRCPlayerApi](#vrcplayerapi) | GetPlayerById(int playerId) | Returns the player with the given id |
| ✔️ | int | GetPlayerCount() | Returns the player count for the instance |
| ✔️ | [VRCPlayerApi[]](#vrcplayerapi) | GetPlayers([VRCPlayerApi[]](#vrcplayerapi) players) | Populates and returns an array with the current players in the instance. The array parameter must be preallocated with at least `VRCPlayerApi.GetPlayerCount` elements. See [example](https://github.com/vrchat-community/UdonSharp/wiki/examples#get-players) |
| | bool | IsOwner([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Shows if the player is the owner of a gameObject with a UdonBehaviour on it |
| | [TrackingData](#trackingdata) | GetTrackingData([TrackingDataType](#trackingdatatype) tt) | Returns the tracking data for the specified type |
| | [Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) | GetBonePosition([HumanBodyBones](https://docs.unity3d.com/ScriptReference/HumanBodyBones.html) bone) | Return position data for the given bone |
| | [Quaternion](https://docs.unity3d.com/ScriptReference/Quaternion.html) | GetBoneRotation([HumanBodyBones](https://docs.unity3d.com/ScriptReference/HumanBodyBones.html) bone) | Return rotation data for the given bone |
| | void | TeleportTo([Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) teleportPos, [Quaternion](https://docs.unity3d.com/ScriptReference/Quaternion.html) teleportRot) | Teleports the player to the position with rotation |
| | void | TeleportTo([Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) teleportPos, [Quaternion](https://docs.unity3d.com/ScriptReference/Quaternion.html) teleportRot, [SpawnOrientation](#spawnorientation) teleportOrientation) | Teleports the player to the position, rotation, and the spawn orientation |
| | void | TeleportTo([Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) teleportPos, [Quaternion](https://docs.unity3d.com/ScriptReference/Quaternion.html) teleportRot, [SpawnOrientation](#spawnorientation) teleportOrientation, bool lerpOnRemote) | Teleports the player to the position, rotation, the spawn orientation, and if you want to lerp on remote |
| | void | EnablePickup(bool enable) | Set if the player can use pickups or not (*Need Testing*) |
| | void | SetPlayerTag(string tagName, string tagValue) | Assigns a value to the tag for the player.  Returns null if the tag has not been assigned.  Note that player tags are not synchronized to remote clients. |
| | string | GetPlayerTag(string tagName) | Returns the value of the given tag for the player. Assign a value of null to clear the tag. |
| | void | ClearPlayerTags() | Clears the tags on the given player |
| | void | SetRunSpeed(float speed) | Sets the player run speed |
| | void | SetWalkSpeed(float speed) | Sets the players walk speed |
| | void | SetJumpImpulse(float impulse) | Sets players jump impulse |
| | void | SetGravityStrength(float strength) | Sets the players gravity |
| | void | SetStrafeSpeed(float speed) | Sets the player's strafe speed.  The default strafe speed is 2.0f. |
| | float | GetRunSpeed() | Returns the current run speed value |
| | float | GetWalkSpeed() | Returns the current walk speed value |
| | float | GetJumpImpulse() | Returns the current jump impulse value |
| | float | GetGravityStrength() | Returns the current gravity value |
| | float | GetStrafeSpeed() | Returns the player's current strafe speed. |
| | bool | IsUserInVR() | Returns if the current user is in VR |
| | void | UseLegacyLocomotion() | Sets the locomotion to the old system |
| | void | Immobilize(bool immobile) | Prevents user from moving |
| | void | UseAttachedStation() | Sits the players down on the station (Requires VRC_Station on the same gameObject) |
| | void | SetVelocity([Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) velocity) | Sets the players velocity |
| | [Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) | GetVelocity() | Returns the player velocity |
| | [Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html) | GetPosition() | Returns the player position |
| | [Quaternion](https://docs.unity3d.com/ScriptReference/Quaternion.html) | GetRotation() | Returns the player rotation |
| | void | SetVoiceGain(float gain) | Add boost to the Player's voice in decibels, range 0-24 |
| | void | SetVoiceDistanceNear(float near) | The near radius, in meters, where volume begins to fall off. It is strongly recommended to leave the Near value at zero for realism and effective spatialization for user voices. In Meters, Range 0 - 1,000,000 |
| | void | SetVoiceDistanceFar(float far) | This sets the end of the range for hearing the user's voice. Default is 25 meters. You can lower this to make another player's voice not travel as far, all the way to 0 to effectively 'mute' the player. In Meters, Range is 0 - 1,000,000 |
| | void | SetVoiceVolumetricRadius(float radius) | A player's voice is normally simulated to be a point source, however changing this value allows the source to appear to come from a larger area. Keep this at zero unless you know what you're doing. In Meters, Range is 0 -1,000. Default 0 |
| | void | SetVoiceLowpass(bool enabled) | When a voice is some distance off, it is passed through a low-pass filter to help with understanding noisy worlds. You can disable this if you want to skip this filter. For example, if you intend for a player to use their voice channel to play a high-quality DJ mix, turning this filter off is advisable. |
| | void | SetAvatarAudioGain(float gain) | Set the Maximum Gain allowed on Avatar Audio. Default is 10. In Decibels, Range 0-10 |
| | void | SetAvatarAudioNearRadius(float distance) | This sets the maximum start of the range for hearing the avatar's audio. Default is 40 meters. You can lower this to make another player's avatar not travel as far, all the way to 0 to effectively 'mute' the player. Note that this is compared to the audio source's minDistance, and the smaller value is used. |
| | void | SetAvatarAudioFarRadius(float distance) | This sets the maximum end of the range for hearing the avatar's audio. Default is 40 meters. You can lower this to make another player's avatar not travel as far, all the way to 0 to effectively 'mute' the player. Note that this is compared to the audio source's maxDistance, and the smaller value is used. |
| | void | SetAvatarAudioVolumetricRadius(float radius) | An avatar's audio source is normally simulated to be a point source, however changing this value allows the source to appear to come from a larger area. This should be used carefully, and is mainly for distant audio sources that need to sound "large" as you move past them. Default is 40 |
| | void | SetAvatarAudioForceSpatial(bool force) | If this is on, then Spatialization is enabled for avatar audio sources, and the spatialBlend is set to 1. Enabling this prevents avatars from using 2D audio. |
| | void | SetAvatarAudioCustomCurve(bool allow) | This sets whether avatar audio sources can use a pre-configured custom curve.  |
| | void | PlayHapticEventInHand([PickupHand](#pickuphand) hand, float duration, float amplitude, float frequency) | Plays haptic feedback on the player's controller for the given hand. |
| | [VRCPickup](#vrcpickup) | GetPickupInHand([PickupHand](#pickuphand) hand) | Returns the associated pickup object for the given hand. |

### InputManager
`static class VRC.SDKBase.InputManager`

#### Methods
| Static | Returns | Name | Summary |
| :---: | --- | --- | --- |
| ✔️ | bool | IsUsingHandController() | Returns whether or not the user is using a hand controller. |
| ✔️ | [VRCInputMethod](#vrcinputmethod) | GetLastUsedInputMethod() | Returns the last input method used, or [`VRCInputMethod.Count`](#vrcinputmethod) if no input method was found. |
| ✔️ | void | EnableObjectHighlight([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj, bool enable) | Enables or disables object highlighting for the specified object. |
| ✔️ | void | EnableObjectHighlight([Renderer](https://docs.unity3d.com/ScriptReference/Renderer.html) r, bool enable) | Enables or disables object highlighting for the specified renderer. |

### SerializationResult
`struct VRC.Udon.Common`

The results returned by the `OnPostSerialization` event.

#### Constructor
| Name | Summary |
| --- | --- |
| SerializationResult(bool success, int byteCount) | Constructor.  Note that this can only be called at editor time. |

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| bool | success | Whether the serialization attempt was successful. |
| int | byteCount | The number of bytes that were serialized. |

### UdonInputEventArgs
`struct VRC.Udon.Common.UdonInputEventArgs`

Provides contextual data for an input event.

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| [UdonInputEventType](#udoninputeventtype) | eventType | The type of input event that was fired. |
| bool | boolValue | The value of the input method when an `InputJump`, `InputUse`, `InputGrab` or `InputDrop` event is fired. |
| float | floatValue | The value of the input method when an `InputMoveHorizontal`, `InputMoveVertical`, `InputLookHorizontal` or `InputLookVertical` event is fired. |
| [HandType](#handtype) | handType | The hand that the input event occurred on. For desktop users, the keyboard is the left hand and the mouse is the right hand. |

### VRCUrl
`class VRC.SDKBase.VRCUrl`

[VRCUrl](#vrcurl) objects currently cannot be constructed at runtime in Udon and are typically constructed at editor time via editor scripts, or retrieved from a [VRCUrlInputField](#vrcurlinputfield).

#### Constructor
| Name | Summary |
| --- | --- |
| VRCUrl(string url) | Constructor that takes a URL as input.  Note that this can only be called at editor time. |

#### Properties
| Static | Type | Name | Summary |
| :---: | --- | --- | --- |
| ✔️ | [VRCUrl](#vrcurl) | Empty | An empty URL. |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| string | Get() | Retrieves the current value of the URL. |

### VRCUrlInputField
`class VRC.SDK3.Components.VRCUrlInputField`

A UI component for end users to input a custom URL and output to Udon programs as a [VRCUrl](#vrcurl).

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| [VRCUrl](#vrcurl) | GetUrl() | Retrieves the current value of the input field. |
| void | SetUrl([VRCUrl](#vrcurl) url) | Sets the URL displayed in the input field. |

### VRCMirrorReflection
`class VRC.SDK3.Components.VRCMirrorReflection` / `class VRC.SDKBase.VRC_MirrorReflection`

A component that manages a mirror surface on an object.

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| bool | m_DisablePixelLights | Disables real-time pixel shaded point and spot lighting. Pixel shaded lights will fall-back to vertex lighting when this is enabled. |
| bool | TurnOffMirrorOcclusion | Disables occlusion culling on the mirror. Enable this if you see objects flickering in the mirror. |
| [LayerMask](https://docs.unity3d.com/ScriptReference/LayerMask.html) | m_ReflectLayers | Only objects on the selected layers will be rendered in the mirror. Objects on the Water layer are never rendered in mirrors. |

### VRCObjectPool
`class VRC.SDK3.Components.VRCObjectPool`

VRC Object Pool provides a lightweight method of managing an array of game objects. The pool will manage and synchronize the active state of each object it holds.

Objects are made active by the pool via the TryToSpawn node, which will return the object that was made active, or a null object if none are available. Objects may be returned to the pool by the pool's owner, and automatically disabled, via the Return node.

When objects are enabled by the pool, the OnEnable event is fired, which an UdonBehaviour on the object may listen for. Note that the OnEnable() event fires before the udon Start() event.

Late joiners will have the objects automatically made active or inactive where appropriate.

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| [GameObject[]](https://docs.unity3d.com/ScriptReference/GameObject.html) | Pool | The objects being managed by this object pool. |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| [GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) | TryToSpawn() | Returns an unused object from the object pool where available, otherwise returns null. |
| void | Return([GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) obj) | Places the specified object back into the pool, freeing it up for future reuse. |

### VRCObjectSync
`class VRC.SDK3.Components.VRCObjectSync`

This component will automatically sync the Transform (position, rotation scale) and Rigidbody (physics) of the object you put it on.

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| bool | AllowCollisionOwnershipTransfer | Should ownership of object transfer if it collides with an object owned by another player. |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| void | SetKinematic(bool value) | Changes the kinematic state, usually handled by the Rigidbody of the object but controlled here for sync purposes. When the kinematic state is on, this Rigidbody ignores forces, collisions and joints. |
| void | SetGravity(bool value) | Changes the gravity state, usually handled by the Rigidbody of the object but controlled here for sync purposes. |
| void | FlagDiscontinuity() | Trigger this when you want to teleport the object - the changes you make this frame will be applied without smoothing. |
| void | TeleportTo([Transform](https://docs.unity3d.com/ScriptReference/Transform.html) targetLocation) | Moves the object to the specified location. |
| void | Respawn() | Moves the object back to its original spawn location. |

### VRCAvatarPedestal
`class VRC.SDK3.Components.VRCAvatarPedestal` / `class VRC.SDKBase.VRC_AvatarPedestal`

A component used to display an avatar in a world, and allows users to switch to the associated avatar.

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| string | blueprintId | Blueprint Id of the avatar to be shown. |
| [Transform](https://docs.unity3d.com/ScriptReference/Transform.html) | Placement | Transform to display the avatar on. |
| bool | ChangeAvatarsOnUse | If set to true, switches the user to the avatar set on the pedestal when used. |
| float | scale | How big or small the avatar should be, only affects the pedestal avatar. |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| void | SwitchAvatar(string id) | Changes the blue print id associated with the pedestal and updates the view for all users. |
| void | SetAvatarUse([VRCPlayerApi](#vrcplayerapi) instigator) | Causes the player to switch to the associated avatar. `instigator` must be the local player as returned by `Networking.LocalPlayer`. |

### VRCPickup
`class VRC.SDK3.Components.VRCPickup` / `class VRC.SDKBase.VRC_Pickup`

A component used to allow objects to be picked up and held.

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| [ForceMode](https://docs.unity3d.com/ScriptReference/ForceMode.html) | MomentumTransferMethod | This defines how the collision force will be added to the other object which was hit, using `Rigidbody.AddForceAtPosition`. Note that the force will only be added if `AllowCollisionTransfer` is on. |
| bool | DisallowTheft | If other users are allowed to take the pickup out of some else's grip. |
| [Transform](https://docs.unity3d.com/ScriptReference/Transform.html) | ExactGun | The position object will be held if set to Exact Gun. |
| [Transform](https://docs.unity3d.com/ScriptReference/Transform.html) | ExactGrip | The position object will be held if set to Exact Grip. |
| bool | allowManipulationWhenEquipped | Should the user be able to manipulate the pickup while the pickup is held if using a controller. |
| [PickupOrientation](#pickuporientation) | orientation | What way the object will be held. |
| [AutoHoldMode](#autoholdmode) | AutoHold | Should the pickup remain in the users hand after they let go of the grab button. |
| string | InteractionText | Tooltip text that is displayed when holding the pickup. |
| string | UseText | Tooltip text that is displayed when hovering over the pickup. |
| float | ThrowVelocityBoostMinSpeed | How fast the object needs to move to be thrown. |
| float | ThrowVelocityBoostScale | How much throwing should scale, higher = faster thrown while lower means slower throw speed. |
| bool | pickupable | Determines whether you can pickup the object. |
| float | proximity | The maximum distance a player can be away from a pickup to interact with it. |
| [VRCPlayerApi](#vrcplayerapi) | currentPlayer | The player that is currently holding the pickup. |
| bool | IsHeld | Determines whether the pickup is currently being held by a player. |
| [PickupHand](#pickuphand) | currentHand | The hand that the player is holding the pickup with. |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| void | Drop() | Drops the pickup if it is being held by a player. |
| void | Drop([VRCPlayerApi](#vrcplayerapi) instigator) | Drops the pickup if it is being held by a player. Note that the pickup will only drop if `instigator` is the player who is holding the pickup. |
| void | GenerateHapticEvent(float duration, float amplitude, float frequency) | Plays haptic feedback on the player's controller. Default values are duration: 0.25, amplitude: 0.5, frequency: 0.5. |
| void | PlayHaptics() | Plays haptic feedback on the player's controller. |

### VRCPortalMarker
`class VRC.SDK3.Components.VRCPortalMarker` / `class VRC.SDKBase.VRC_PortalMarker`

A component used to create portals to other rooms.

#### Properties
| Type | Name | Summary |
| --- | --- | --- |
| string | roomId | Room Id of the destination room. |

#### Methods
| Returns | Name | Summary |
| --- | --- | --- |
| void | RefreshPortal() | Refreshes the portal displayed to the player. |


## Enums

### EventTiming
`enum VRC.Udon.Common.Enums.EventTiming`

| Name | Summary |
| --- | --- |
| Update | The event is fired during the `Update()` event. |
| LateUpdate | The event is fired during the `LateUpdate()` event. |

### Mobility
`enum VRC.SDKBase.VRCStation.Mobility`

| Name | Summary |
| --- | --- |
| Mobile | Allow users to move when seated in station |
| Immobilize | Prevents user from moving |
| ImmobilizeForVehicle | Same as Immobilized but optimized for moving stations |

### NetworkEventTarget
`enum VRC.Udon.Common.Interfaces.NetworkEventTarget`

| Name | Summary |
| --- | --- |
| All  | All players in the instance |
| Owner | Owner of the game object |

### SpawnOrientation
`enum VRC.SDKBase.VRC_SceneDescriptor.SpawnOrientation`

| Name | Summary |
| --- | --- |
| Default  | Use the VRChat default spawn behaviour (currently the same as AlignPlayerWithSpawnPoint) |
| AlignPlayerWithSpawnPoint | Aligns player with the rotation of the spawn transform |
| AlignRoomWithSpawnPoint | Aligns players room scale to be centered on spawn point |

### TrackingDataType
`enum VRC.SDKBase.VRCPlayerApi.TrackingDataType`

| Name | Summary |
| --- | --- |
| Head | The player's head tracking data |
| LeftHand | The player's left hand tracking data |
| RightHand | The player's right hand tracking data |
| Origin | The player's playspace origin |

### VRCInputMethod
`enum VRC.SDKBase.VRCInputMethod`

| Name | Value | Summary |
| --- | --- | --- |
| Keyboard | 0 | Keyboard input method |
| Mouse | 1 | Mouse input method |
| Controller | 2 | Controller input method |
| Gaze | 3 | Gaze input method |
| Vive | 5 | Vive input method |
| Oculus | 6 | Oculus input method |
| Count | 7 | Maximum number of input methods available. |

### HandType
`enum VRC.Udon.Common.HandType`

| Name | Summary |
| --- | --- |
| RIGHT | Right hand |
| LEFT | Left hand |

### UdonInputEventType
`enum VRC.Udon.Common.UdonInputEventType`

| Name | Summary |
| --- | --- |
| BUTTON | Button event |
| AXIS | Axis event |

### VideoError
`enum VRC.SDK3.Components.Video.VideoError`

| Name | Summary |
| --- | --- |
| Unknown | Unknown error |
| InvalidURL | Invalid URL |
| AccessDenied | Access Denied |
| PlayerError | Player Error |
| RateLimited | Rate Limited |

### AutoHoldMode
`enum VRC.SDK3.Components.VRCPickup.AutoHoldMode` / `enum VRC.SDKBase.VRC_Pickup.AutoHoldMode`

| Name | Summary |
| --- | --- |
| AutoDetect | Automatically detect which behaviour to apply. |
| Yes | After the grab button is released the pickup remains in the hand until the drop button is pressed and released. |
| No | After the grab button is released the pickup is let go. |

### PickupOrientation
`enum VRC.SDK3.Components.VRCPickup.PickupOrientation` / `enum VRC.SDKBase.VRC_Pickup.PickupOrientation`

| Name | Summary |
| --- | --- |
| Any | Any orientation |
| Grip | Grip orientation |
| Gun | Gun orientation |

### PickupHand
`enum VRC.SDK3.Components.VRCPickup.PickupHand` / `enum VRC.SDKBase.VRC_Pickup.PickupHand`

| Name | Summary |
| --- | --- |
| None | No hand |
| Left | Left hand |
| Right | Right hand |


# Supported Features

## Synced Variables
These variables are available for syncing across the network with the [UdonSynced](https://udonsharp.docs.vrchat.com/udonsharp/#udonsynced) attribute.
:::note
In the lists below, 'size' refers to the **approximate** size in memory. When networked, the data is serialized, which may lead to more data being transmitted. For example, syncing a `bool` will send **at least** 1 byte of data (instead of 1 bit) in addition to any networking overhead.
To find out how many bytes of serialized data were, use `byteCount` in the [`OnPostSerialization`](https://docs.vrchat.com/docs/network-components#onpostserialization) event. You can find more information about syncing on Udon's [Network Specs](https://docs.vrchat.com/docs/network-details#data-and-specs) page.
:::
### Boolean  types
| Type | Size    |
| ---- | ------- |
| bool | 1 byte  |
### Integral numeric types
| Type   | Range                           | Size    |
|--------|---------------------------------|---------|
| sbyte  | -128 to 127                     | 1 byte  |
| byte   | 0 to 255                        | 1 byte  |
| short  | -32,768 to 32,767               | 2 bytes |
| ushort | 0 to 65,535                     | 2 bytes |
| int    | -2,147,483,648 to 2,147,483,647 | 4 bytes |
| uint   | 0 to 4,294,967,295              | 4 bytes |
| long   | -9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 | 8 bytes |
| ulong  | 0 to 18,446,744,073,709,551,615 | 8 bytes |
### Floating-point numeric types
| Type   | Approximate range             | Precision     | Size    |
|--------|-------------------------------|---------------|---------|
| float  | ±1.5 x 10^(−45) to ±3.4 x 10^(38)   | ~6-9 digits   | 4 bytes |
| double | ±5.0 × 10^(−324) to ±1.7 × 10^(308) | ~15-17 digits | 8 bytes |
### Vector mathematics types and structures (Unity)
| Type        | Range         | Size     |
|-------------|---------------|----------|
| [Vector2](https://docs.unity3d.com/ScriptReference/Vector2.html)   | same as float | 8 bytes  |
| [Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html)   | same as float | 12 bytes  |
| [Vector4](https://docs.unity3d.com/ScriptReference/Vector4.html)   | same as float | 16 bytes |
| [Quaternion](https://docs.unity3d.com/ScriptReference/Quaternion.html)| same as float | 16 bytes  |
### Color structures
| Type     | Range / Precision | Size    |
|----------|-------------------|---------|
| [Color](https://docs.unity3d.com/ScriptReference/Color.html)  | same as float     | 16 bytes |
| [Color32](https://docs.unity3d.com/ScriptReference/Color32.html)| same as byte      | 4 bytes |
### Text types and structures
| Type   | Range            | Size           |
|--------|------------------|----------------|
| char   | U+0000 to U+FFFF | 2 bytes        |
| string | same as char     | 2 bytes / char |
### Other structures
| Type   | Range            | Size           |
|--------|------------------|----------------|
| [VRCUrl](#vrcurl) | U+0000 to U+FFFF | 2 bytes / char |
