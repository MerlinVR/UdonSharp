# Events

* [Udon Events](#udon-events)
* [Unity Events](#unity-events)

# Udon Events
These are the method stubs you can override via `UdonSharpBehaviour`.
 - [VRChat Event Documentation](https://docs.vrchat.com/docs/event-nodes)

These methods **have** to be public.

```cs
public override void <method>() {}
```



## Udon Update Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|PostLateUpdate()|Fired near the end of the frame after IK has been calculated. Getting bone positions at this time will give you the most up to date positions so that they are not a frame behind.|
## Udon Input Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|Interact()|Fired when a user interacts with the object<br/>Will add a box collider if no collider is present.|
|`void`|InputJump(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputUse(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputDrop(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputMoveHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputMoveVertical(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputLookHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputLookVertical(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|

## Udon Pickup Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|OnDrop()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|
|`void`|OnPickup()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|
|`void`|OnPickupUseDown()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|
|`void`|OnPickupUseUp()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|


## Udon Networking Events
| Return type | Name | Description |
| --- | --- | --- |
|`bool`|OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)|Fired when an UdonBehaviour is going to change owner.  Returning `true` will accept the change in ownership, and returning `false` will reject the change in ownership.|
|`void`|OnOwnershipTransferred(VRCPlayerApi player)|Fired every time a UdonBehaviour changes owner|
|`void`|OnPreSerialization()|Fired before sending network data|
|`void`|OnPostSerialization(VRC.Udon.Common.SerializationResult result)|Fired after sending network data and provides data on whether the serialization attempt succeeded and how many bytes were serialized.|
|`void`|OnDeserialization()|Fired when network data is received|

## Udon Player Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|OnPlayerJoined(VRCPlayerApi player)|Fired when a new player joins the instance|
|`void`|OnPlayerLeft(VRCPlayerApi player)|Fired when a player leaves the instance|
|`void`|OnPlayerRespawn(VRCPlayerApi player)|Fired when a player respawns|
|`void`|OnPlayerTriggerEnter(VRCPlayerApi player)|Player enters a trigger|
|`void`|OnPlayerTriggerStay(VRCPlayerApi player)|Player stays in a trigger|
|`void`|OnPlayerTriggerExit(VRCPlayerApi player)|Player leaves a trigger|
|`void`|OnPlayerCollisionEnter(VRCPlayerApi player)|Player collides with a collider|
|`void`|OnPlayerCollisionStay(VRCPlayerApi player)|Player stays on a collider|
|`void`|OnPlayerCollisionExit(VRCPlayerApi player)|Player leaves the collider|
|`void`|OnPlayerParticleCollision(VRCPlayerApi player)|A collision particle hits the player|
|`void`|OnControllerColliderHitPlayer(ControllerColliderPlayerHit hit)|CharacterController collides with a player|

## Udon Station Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|OnStationEntered(VRCPlayerApi player)|Requires [VRC_Station](https://docs.vrchat.com/docs/vrc_station)|
|`void`|OnStationExited(VRCPlayerApi player)|Requires [VRC_Station](https://docs.vrchat.com/docs/vrc_station)|

## Udon Video Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|OnVideoEnd()|When the video player ends playback |
|`void`|OnVideoError(VideoError videoError)|When an error occurs with the player |
|`void`|OnVideoLoop()|If looping is enabled, this will fire at the end|
|`void`|OnVideoPause()|When the video player is paused |
|`void`|OnVideoPlay()|When you start playing a video |
|`void`|OnVideoStart()|When a video is playing for the first time? |
|`void`|OnVideoReady()|When the video player has loaded the url|

## Udon MIDI Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|MidiNoteOn(int channel, int number, int velocity)|Triggered when a Note On message is received, typically by pressing a key / pad on your device. See [Midi in Udon](https://docs.vrchat.com/docs/midi) for more information.|
|`void`|MidiNoteOff(int channel, int number, int velocity)|Triggered when a Note Off message is received, typically by releasing a key / pad on your device. See [Midi in Udon](https://docs.vrchat.com/docs/midi) for more information.|
|`void`|MidiControlChange(int channel, int number, int value)|Triggered when a control change is received. These are typically sent by knobs and sliders on your Midi device. See [Midi Events](https://docs.vrchat.com/docs/midi) for more information.|

## Udon String/Image Loading Events
| Return type | Name | Description |
| --- | --- | --- |
|`void`|OnImageLoadSuccess(IVRCImageDownload result)|Triggered when an image download succeeds. See [Image Loading](https://docs.vrchat.com/docs/image-loading).|
|`void`|OnImageLoadError(IVRCImageDownload result)|Triggered when an image download fails. See [Image Loading](https://docs.vrchat.com/docs/image-loading).|
|`void`|OnStringLoadSuccess(IVRCStringDownload result)|Triggered when a string download succeeds. See [String Loading](https://docs.vrchat.com/docs/string-loading).|
|`void`|OnStringLoadError(IVRCStringDownload result)|Triggered when a string download fails. See [String Loading](https://docs.vrchat.com/docs/string-loading).|

# Unity Events
These are the method stubs available for Unity events.
[Unity MonoBehaviour Documentation](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html)

- FixedUpdate
- LateUpdate
- OnCollisionEnter2D
- OnAnimatorIK
- OnAnimatorMove
- OnAudioFilterRead
- OnBecameVisible
- OnBecameInvisible
- OnCollisionEnter
- OnCollisionExit
- OnCollisionExit2D
- OnCollisionStay
- OnCollisionStay2D
- OnControllerColliderHit
- OnDestroy
- OnDrawGizmos
- OnDrawGizmosSelected
- OnDisable
- OnEnable
- OnJointBreak
- OnJointBreak2D
- OnMouseDown
- OnMouseDrag
- OnMouseEnter
- OnMouseExit
- OnMouseOver
- OnMouseUp
- OnMouseUpAsButton
- OnParticleCollision
- OnParticleTrigger
- OnPostRender
- OnPreCull
- OnPreRender
- OnRenderImage
- OnRenderObject
- OnTransformChildrenChanged
- OnTransformParentChanged
- OnTriggerEnter
- OnTriggerEnter2D
- OnTriggerExit
- OnTriggerExit2D
- OnTriggerStay
- OnTriggerStay2D
- OnValidate
- OnWillRenderObject
- Reset
- Start
- Update
