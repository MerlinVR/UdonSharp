# Community Resources

## Tutorials and info

### はつぇさんのブログ
- [U# 入門 ①](https://hatuxes.hatenablog.jp/entry/2020/04/05/013310)
- [U# 入門 ②](https://hatuxes.hatenablog.jp/entry/2020/04/05/013323)
- [U# 入門 ③](https://hatuxes.hatenablog.jp/entry/2020/04/05/013336)
- [U# 入門 おまけ](https://hatuxes.hatenablog.jp/entry/2020/04/05/013348)

### やぎりさんのブログ
- [UdonSharp走り書きメモ.cs（執筆中、順次更新）](https://yagiri000.hatenablog.com/entry/2020/04/04/162312)

### Vowgan's Tutorial Videos

These videos start with the graph in the first half and cover U# in the second half
- [VRChat Udon Tutorial | Basic Buttons](https://www.youtube.com/watch?v=GWv3zloRWY4)
- [VRChat Udon Tutorial | Contextual Buttons](https://www.youtube.com/watch?v=01a5qO60qlo)
- [VRChat Udon Tutorial | Jumping and PlayerMods](https://www.youtube.com/watch?v=OventaglGCY)

## Tools
### orels1's UdonToolKit
Provides a number of useful utility behaviours and a much more powerful attribute system for making custom inspectors for your U# behaviours.

https://github.com/orels1/UdonToolkit/

### cannorin's extern search
This is fairly out of date at this point since it hasn't had the node registry updated in a while.
This is a web tool that lets you search what functions are available to Udon
https://7colou.red/UdonExternSearch/

### CyanEmu
CyanEmu is a VRChat client emulator that enables you to test and debug your Udon (and SDK2) VRChat worlds directly in Unity. It comes with a desktop player controller that can use interacts, grab pickups, sit in chairs, respawn, etc.

https://github.com/CyanLaser/CyanEmu

### Phasedragon's Input table

This lists all of the inputs that VRChat currently binds and how they work with each VR controller. Inputs that return true or false can be read using `Input.GetButton()` with the listed name for the input. Inputs that return somewhere in the range -1 to 1 or 0 to 1 can be read using `Input.GetAxis()` or `Input.GetAxisRaw()`

https://docs.google.com/spreadsheets/d/1_iF0NjJniTnQn-knCjb5nLh6rlLfW_QKM19wtSW_S9w/edit#gid=1150012376

If you would like to test a controller not on the list, you can go to my input test world

https://vrchat.com/home/world/wrld_f8d5f7e4-185c-4b82-8ecb-8ae0c7953085

### Shatoo's Udon editor debug 
Provides a UI to call built-in events with arguments

https://shatoo.booth.pm/items/1958756

### Jordo's Haptics Testing world
Provides 3 sliders to adjust haptics and test how they feel for later use in your code.

https://vrchat.com/home/launch?worldId=wrld_7f010f63-7a82-4668-b1a5-412b57fb08f5&instanceId=0
