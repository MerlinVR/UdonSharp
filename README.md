# UdonSharp

UdonSharp is a compiler that translates C# to Udon assembly, so you can create interactive VRChat worlds by writing C# code.

You can find the full docs at [https://udonsharp.docs.vrchat.com](https://udonsharp.docs.vrchat.com).

# :warning: IMPORTANT! :warning:
The old 0.x version of UdonSharp which used to be delivered as a .unitypackage is deprecated and no longer supported. You can use the [Creator Companion](https://vcc.docs.vrchat.com/) to migrate your projects to this new version and keep it updated moving forward.

## Requirements
- Unity 2019.4.31f1
- VRChat World SDK (can be automatically resolved, see below)

## Getting Started

If you're comfortable using GitHub, follow the directions in the [UdonSharp Template repo](https://github.com/vrchat-community/template-udonsharp) to quickly make your own ready-to-use repository.
Otherwise, you can [download the VRChat Creator Companion](https://vrchat.com/home/download), and then choose Projects > New > UdonSharp to create a new ready-to-use Unity Project.

Use Unity 2019.4.31.f1 to open the project. Press "OK" on the dialog that offers to download the required VRChat packages.

![image](https://user-images.githubusercontent.com/737888/185468226-33492169-c1f5-4b27-b5c4-83febb5e6e66.png)


## Loading the Example World

Find the "VRChat SDK" item in the menu bar at the top of the Unity Editor window, press it to open, then choose "Samples > UdonExampleScene".

![samples-udonexample-scene](https://user-images.githubusercontent.com/737888/186485286-2758cec3-ec89-4598-a451-9fa12fa27616.png)

Once the scene opens, choose "File > Save As..." and give the scene a new name.

Then modify the scene however you'd like - you learn about all the examples in [the UdonExampleScene](https://docs.vrchat.com/docs/udon-example-scene) or learn about [Getting Started with Udon](https://docs.vrchat.com/docs/getting-started-with-udon).

## Making your own Scripts

1. Make a new object in your scene
2. Add an `Udon Behaviour` component to your object
3. Below the "New Program" button click the dropdown and select "Udon C# Program Asset"
4. Now click the New Program button, this will create a new UdonSharp program asset for you
5. Click the Create Script button and choose a save destination and name for the script.
6. This will create a template script that's ready for you to start working on, open the script in your editor of choice and start programming.


   Instead of creating assets from an UdonBehaviour you can also do the following:
1. Right-click in your project asset explorer
2. Navigate to Create > U# script 
3. Click U# script, this will open a create file dialog
4. Choose a name for your script and click Save
5. This will create a .cs script file and an UdonSharp program asset that's set up for the script in the same directory

## Test Your World
When you're ready to try out your World, find and choose the menu item "VRChat SDK > Show Control Panel".
* Sign into your VRChat Account in the "Authentication" tab.
* Switch to the "Builder" tab and choose "Build & Test".
* After a quick build process, VRChat should open up in your test world!
* If you have any issues making a test world, check out [our docs on Using Build & Test](https://docs.vrchat.com/docs/using-build-test).

## Publish Your World

When you're ready to publish your World so you can use it regularly:
* Return to the VRChat SDK Control Panel in your Unity Project
* Switch to the "Builder" tab and press "Build and Publish for Windows".
* This will build your World and add some publishing options to your Game window.
* Fill out the fields "World Name", "Description" and "Sharing", and check the terms box "the above information is accurate...".
* Press "Upload".

Return to VRChat - open the "Worlds" menu, then scroll down to the section named "Mine". Choose your world from the list and press "Go" to check it out!

## Visual Studio Code Integration

You can follow [this video guide](https://www.youtube.com/watch?v=ihVAKiJdd40) to get Intellisense for C# within your Unity project in Visual Stido Code (VS Code), inlcuding libraries such as UdonSharp.
  
## Credits

- See [CONTRIBUTORS.md](https://github.com/vrchat-community/UdonSharp/blob/master/CONTRIBUTORS.md) for people who have helped provide improvments to UdonSharp
- The open source project [Harmony](https://github.com/pardeike/Harmony) helps Udonsharp provide a better editor experience

# 
[![Discord](https://img.shields.io/badge/Discord-Merlin%27s%20Discord%20Server-blueviolet?logo=discord)](https://discord.gg/Ub2n8ZA)
