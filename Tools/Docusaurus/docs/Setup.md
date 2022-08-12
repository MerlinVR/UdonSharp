# Setup

**Requirements**
- [Unity 2019.4.31f1](https://unity3d.com/get-unity/download/archive)
- [VRCSDK3 + Udon](https://vrchat.com/home/download)
- [UdonSharp](https://github.com/Merlin-san/UdonSharp/releases)

**Installation**

> NOTE: This will be easier soon with the release of the VRChat Creator Companion. These notes are for the legacy ".unitypackage" format.

- Read the official [Getting Started With Udon](https://ask.vrchat.com/t/getting-started-with-udon/80) thread, this has basic installation instructions for Udon.
- Install the latest version of [VRCSDK3 + Udon](https://vrchat.com/home/download).
- Get the [latest release of UdonSharp](https://github.com/Merlin-san/UdonSharp/releases) and install it in your project.

**Getting started**

1. Make a new object in your scene
2. Add an Udon Behaviour component to your object
3. Below the "New Program" button click the dropdown and select "Udon C# Program Asset"
4. Now click the New Program button, this will create a new UdonSharp program asset for you
5. Click the Create Script button and choose a save destination and name for the script.
6. This will create a template script that's ready for you to start working on, open the script in your editor of choice and start programming

**Asset explorer asset creation**

Instead of creating assets from an UdonBehaviour you can also do the following:
1. Right-click in your project asset explorer
2. Navigate to Create > U# script
3. Click U# script, this will open a create file dialog
4. Choose a name for your script and click Save
5. This will create a .cs script file and an UdonSharp program asset that's set up for the script in the same directory