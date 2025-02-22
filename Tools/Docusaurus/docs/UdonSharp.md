# UdonSharp

# Attributes
All supported attributes in UdonSharp

| | Attribute | | 
|--- | --- | --- |
|[Header](https://docs.unity3d.com/ScriptReference/HeaderAttribute.html)|[HideInInspector](https://docs.unity3d.com/ScriptReference/HideInInspector.html)|[NonSerialized](https://docs.microsoft.com/dotnet/api/system.nonserializedattribute)|
|[SerializeField](https://docs.unity3d.com/ScriptReference/SerializeField.html)|[Space](https://docs.unity3d.com/ScriptReference/SpaceAttribute.html)|[Tooltip](https://docs.unity3d.com/ScriptReference/TooltipAttribute.html)|
|[ColorUsage](https://docs.unity3d.com/ScriptReference/ColorUsageAttribute.html)|[GradientUsage](https://docs.unity3d.com/ScriptReference/GradientUsageAttribute.html)|[TextArea](https://docs.unity3d.com/ScriptReference/TextAreaAttribute.html)|
|[UdonSynced](#udonsynced)|[DefaultExecutionOrder](#defaultexecutionorder)|[UdonBehaviourSyncMode](#udonbehavioursyncmode)|
|[RecursiveMethod](#recursivemethod)|[FieldChangeCallback](#fieldchangecallback)|


## UdonSynced
`[UdonSynced]` / `[UdonSynced(UdonSyncMode)]`

*See [Synced Variables](/vrchat-api#synced-variables) for variables that can be synced.*

### Example
```cs
public class Example : UdonSharpBehaviour 
{
    [UdonSynced]
    public bool synchronizedBoolean;

    [UdonSynced(UdonSyncMode.Linear)]
    // This float will be linearly interpolated
    public float synchronizedFloat;
}
```

### UdonSyncMode
`UdonSharp.UdonSyncMode`

| Name | Summary |
| --- | --- |
| NotSynced | |
| None | No interpolation (Default) |
| Linear | Lerp |
| Smooth | *Some kind of smoothed syncing* |

## UdonBehaviourSyncMode
`[UdonBehaviourSyncMode]` / `[UdonBehaviourSyncMode(BehaviourSyncMode)]`

Enforces a chosen sync mode and performs additional validation on synced variables where appropriate.

### Example
```cs
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class Example : UdonSharpBehaviour 
{ 
}
```

### BehaviourSyncMode
`UdonSharp.BehaviourSyncMode`

| Name | Summary |
| --- | --- |
| Any | Nothing is enforced and the behaviours can be set to either sync type by the user. This is the default when no attribute is specified. |
| None | Enforces no synced variables on the behaviour and hides the selection dropdown in the UI for the sync mode. Nothing is synced and SendCustomNetworkEvent will not work on the behaviour. |
| Continuous | Synced variables will be updated automatically at a very frequent rate, but may not always reliably update to save bandwidth. |
| Manual | Synced variables are updated manually by the user less frequently, but ensures that updates are reliable when requested. |
| NoVariableSync | Enforces that there are no synced variables on the behaviour, hides the sync mode selection dropdown, and allows you to use the behaviours on GameObjects that use either Manual or Continuous sync. |

## DefaultExecutionOrder

Specifies the order that Update, LateUpdate, and FixedUpdate happen in relative to other UdonSharpBehaviours with an int. All behaviours are at 0 by default, the lower the int, the earlier their update happens. The int can be negative.

### Example
```cs
[DefaultExecutionOrder(0)]
public class Example : UdonSharpBehaviour 
{ 
}
```

## RecursiveMethod
`[RecursiveMethod]`

Marks a method as callable recursively. This means the marked method can safely call itself on the same behaviour without issues. This does have a performance overhead, so only use it on methods that you know may be called recursively.

### Example
```cs
[RecursiveMethod]
int Factorial(int input)
{
    if (input == 1)
        return 1;

    return input * Factorial(input - 1);
}
```

## FieldChangeCallback
`[FieldChangeCallback(string)]`

This is an attribute that you may put on a field in order to receive Udon variable change events. This attribute takes a string parameter that points to a property name on the behaviour. When this attribute is set on a field, any modification to the field via network sync or SetProgramVariable will call the target property's setter instead of setting the field. The property is usually expected to set the field in this case.

### Example
```cs
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ExampleOfFieldChangeCallback: UdonSharpBehaviour
{
    public GameObject toggleObject;

    [UdonSynced, FieldChangeCallback(nameof(SyncedToggle))]
    private bool _syncedToggle;

    public bool SyncedToggle
    {
        set
        {
            Debug.Log("toggling the object...");
            _syncedToggle = value;
            toggleObject.SetActive(value);
        }
        get => _syncedToggle;
    }

    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        SyncedToggle = !SyncedToggle;
        RequestSerialization();
    }
}
```

Note that in the above example, the Interact performs ```SyncedToggle = !SyncedToggle;``` rather than ```_syncedToggle = !_syncedToggle```. The latter won't work (this would not actually trigger the FieldChangeCallback). FieldChangeCallback only will cause SyncedToggle's setter to fire when either SetProgramVariable or a network sync updates the value of syncedToggle. It will not fire when the variable is set directly from inside the same UdonBehaviour. The property should always be used directly. UdonSharp will deliberately fail to compile if you attempt to set ```_syncedToggle``` from outside the UdonBehaviour. In this case, the property should be used or SetProgramVariable should be used explicitly.
