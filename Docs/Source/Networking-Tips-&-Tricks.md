# Networking Tips & Tricks

* [Ownership](#ownership)
* [Events](#events)
* [Synced Variables](#synced-variables)
* [Instantiation](#instantiation)

---

# Preamble

Networking is a huge WIP in Udon and currently very finicky. Unless you're up for a challenge you should keep things very very simple.
The amount of data you can send in a limited time is heavily restricted by VRChat. Sending too much data will lead to "Death Runs" (not all data could be sent and some is lost).

# Ownership

Ownership in VRChat works on a per-GameObject basis. This means that all GameObjects that have an UdonBehaviour also have an Owner on the Network. (Note: There is other non-UdonBehaviour things that have owners, please note them here).
The owner of a GameObject is by default the instance master, which is the player that has been in the instance the longest. Ownership can however be transferred manually via Networking.SetOwner and is sometimes transferred by other means, such as picking up a VRC_Pickup will automatically make you the owner, if the Pickup is networked (?).
As the owner of an object you constantly send the synced data to other players. This can be transform data for position sync, synced variables and other data.

## Known Issues:
When transferring ownership you cannot set synced variables immediately after setting ownership. The old owner has to acknowledge that you have become the new owner first (Note: This is a bug that is being looked at. Not sure if that is exactly what happens, but a suspicion). Depending on ping this can take up to a few seconds.

# Events

Networked events are expressed as Photon RPCs. They are sent to the specified UdonBehaviour and either all players or the owner of the GameObject the targeted UdonBehaviour is attached to. There currently is no direct way of targeting a specific player.
### Workaround 1:
Give each Player ownership over a GameObject, such that each player now has a single UdonBehaviour that they are associated with. Via Networking.GetOwner(GameObject) you can find out which UdonBehaviour to sent the event to to target the specified player. This method is very complex to setup.
### Workaround 2:
Use a synced variable (string for displayName or int for playerID) to tell everyone which player is meant. This method however is a little tricky due to the problem with synced variable/network event interactions described further below.

## Known Issues:
Sending too many events can "block" the Network. 
When used in combination with networked events, events are usually processed and received much quicker than synced variable updates. This means that trying to set a synced variable and then sending an event will usually result in the event arriving before the update. To avoid this you have to either wait a sufficient amount of time after updating a variable before sending the event or detecting the variable change locally.

# Synced Variables
Synced variables are updated periodically by the owner. Due to the restrictions on the amount of data that can be synced, synced variables are especially costly. However they are also limited per-variable. This is especially noticeable with strings as the max character length is around 50-ish.

# Instantiation
Instantiation is currently (2020-06-17) not networked. Instantiated objects cannot be correctly synchronized.
The only workaround is to use object-pooling. (Note: Please write a more detailed guide on that)