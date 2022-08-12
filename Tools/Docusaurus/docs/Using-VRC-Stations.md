# Using VRC Stations

### Making a chair to sit in

Making a chair to sit in is fairly straightforward and the **VRCChair3** prefab included with the VRCSDK shows how to setup one. 

All you need for a chair is a GameObject with the following components:
1. VRC_Station with an entry and exit point set to itself or another transform that designates there the player is rooted to the station
2. A collider, usually with IsTrigger enabled
3. An UdonBehaviour with an Udon program that handles sitting in the station
4. Optionally a mesh attached it that looks like a chair

The VRCSDK comes with a program that handles #3 called **StationGraph**, the equivalent U# code for that graph is:
```cs
public override void Interact()
{
    Networking.LocalPlayer.UseAttachedStation();
}
```

### Detecting when a player enters or exits a station

For making vehicles it can be useful to know when a player has entered or exited a station. Udon provides events for when players enter and exit stations for the situation where you want this info. 

If you haven't already, make a U# program. Add a way to enter the station if you want, this can be done in the way noted above with the Interact event.

In order to receive the enter and exit events, you need to add these events to the behaviour, they can be added by adding this code:

```cs
public override void OnStationEntered(VRCPlayerApi player)
{
}

public override void OnStationExited(VRCPlayerApi player)
{
}
```

Now you can use the `player` variable to check what player has entered/exited the station. You can also use this to check if the player is the local player using `player.isLocal`.