# Events

* [Unity Events](#unity-events)
* [Udon Events](#udon-events)

# Unity Events
These are the method stubs available for Unity events.
 - [Unity MonoBehaviour Documentation](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html)

|  | Events | |
| --- | --- | --- |
| FixedUpdate | OnJointBreak | OnRenderImage |
| LateUpdate | OnJointBreak2D | OnRenderObject |
| OnAnimatorIK | OnMouseDown | OnTransformChildrenChanged |
| OnAnimatorMove | OnMouseDrag | OnTransformParentChanged |
| OnCollisionEnter | OnMouseEnter | OnTriggerEnter |
|OnCollisionEnter2D | OnMouseExit | OnTriggerEnter2D |
| OnCollisionExit | OnMouseOver | OnTriggerExit |
| OnCollisionExit2D | OnMouseUp | OnTriggerExit2D |
| OnCollisionStay | OnMouseUpAsButton | OnTriggerStay |
| OnCollisionStay2D | OnParticleCollision | OnTriggerStay2D |
| OnControllerColliderHit | OnParticleTrigger | OnWillRenderObject |
| OnDestroy | OnPostRender | Start |
| OnDisable | OnPreCull | Update |
| OnEnable | OnPreRender | |

# Udon Events
These are the method stubs you can override via `UdonSharpBehaviour`.
 - [VRChat Event Documentation](https://docs.vrchat.com/docs/event-nodes)

These methods **have** to be public

```cs
public override void <method>() {}
```

Returns | Name | |
| --- | --- | --- |
|`void`|PostLateUpdate()|Fired near the end of the frame after IK has been calculated. Getting bone positions at this time will give you the most up to date positions so that they are not a frame behind.|
|`void`|Interact()|Fired when a user interacts with the object<br/>Will add a box collider if no collider is present.|
|`void`|OnDrop()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|
|`bool`|OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)|Fired when an UdonBehaviour is going to change owner.  Returning `true` will accept the change in ownership, and returning `false` will reject the change in ownership.|
|`void`|OnOwnershipTransferred(VRCPlayerApi player)|Fired every time a UdonBehaviour changes owner|
|`void`|OnPickup()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|
|`void`|OnPickupUseDown()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|
|`void`|OnPickupUseUp()|Requires [VRC_Pickup](https://docs.vrchat.com/docs/vrc_pickup)|
|`void`|OnPlayerJoined(VRCPlayerApi player)|Fired when a new player joins the instance|
|`void`|OnPlayerLeft(VRCPlayerApi player)|Fired when a player leaves the instance|
|`void`|OnPlayerRespawn(VRCPlayerApi player)|Fired when a player respawns|
|`void`|OnSpawn()|Fired when a object is spawned via network instantiation|
|`void`|OnStationEntered(VRCPlayerApi player)|Requires [VRC_Station](https://docs.vrchat.com/docs/vrc_station)|
|`void`|OnStationExited(VRCPlayerApi player)|Requires [VRC_Station](https://docs.vrchat.com/docs/vrc_station)|
|`void`|OnVideoEnd()|When the video player ends playback |
|`void`|OnVideoError(VideoError videoError)|When an error occurs with the player |
|`void`|OnVideoLoop()|If looping is enabled, this will fire at the end|
|`void`|OnVideoPause()|When the video player is paused |
|`void`|OnVideoPlay()|When you start playing a video |
|`void`|OnVideoStart()|When a video is playing for the first time? |
|`void`|OnVideoReady()|When the video player has loaded the url|
|`void`|OnPreSerialization()|Fired before sending network data|
|`void`|OnPostSerialization(VRC.Udon.Common.SerializationResult result)|Fired after sending network data and provides data on whether the serialization attempt succeeded and how many bytes were serialized.|
|`void`|OnDeserialization()|Fired when network data is received|
|`void`|OnPlayerTriggerEnter(VRCPlayerApi player)|Player enters a trigger|
|`void`|OnPlayerTriggerStay(VRCPlayerApi player)|Player stays in a trigger|
|`void`|OnPlayerTriggerExit(VRCPlayerApi player)|Player leaves a trigger|
|`void`|OnPlayerCollisionEnter(VRCPlayerApi player)|Player collides with a collider|
|`void`|OnPlayerCollisionStay(VRCPlayerApi player)|Player stays on a collider|
|`void`|OnPlayerCollisionExit(VRCPlayerApi player)|Player leaves the collider|
|`void`|OnPlayerParticleCollision(VRCPlayerApi player)|A collision particle hits the player|
|`void`|MidiNoteOn(int channel, int number, int velocity)|Triggered when a Note On message is received, typically by pressing a key / pad on your device. See [Midi in Udon](https://docs.vrchat.com/docs/midi) for more information.|
|`void`|MidiNoteOff(int channel, int number, int velocity)|Triggered when a Note Off message is received, typically by releasing a key / pad on your device. See [Midi in Udon](https://docs.vrchat.com/docs/midi) for more information.|
|`void`|MidiControlChange(int channel, int number, int value)|Triggered when a control change is received. These are typically sent by knobs and sliders on your Midi device. See [Midi Events](https://docs.vrchat.com/docs/midi) for more information.|
|`void`|InputJump(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputUse(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputDrop(bool value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputMoveHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputMoveVertical(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputLookHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|
|`void`|InputLookVertical(float value, VRC.Udon.Common.UdonInputEventArgs args)|See [Input Events](https://docs.vrchat.com/docs/input-events) for more information.|