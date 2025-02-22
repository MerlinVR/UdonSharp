# Setup

**Requirements**
- [Unity 2019.4.31f1](https://unity3d.com/get-unity/download/archive)
- [VRCSDK3 + Udon](https://vrchat.com/home/download)

**Installation**

You can get UdonSharp by using the [VRChat Creator Companion](https://vcc.docs.vrchat.com/) (also known as the VCC), its [CLI](https://vcc.docs.vrchat.com/vpm/cli/), or a [starter template](https://github.com/vrchat-community/template-udonsharp). 

## Create a new UdonSharp project with the VCC:
- Install the latest version of the [Creator Companion](https://vrchat.com/home/download).
- From the main screen, select "New", then "UdonSharp", and choose a directory.
- Press "Open Project". That's it!

## Create a new UdonSharp Project with Source Control:
- Visit the [UdonSharp Project Template repository](https://github.com/vrchat-community/template-udonsharp).
- Press "Use this template".
- Clone the project to your computer using your favorite Git client.
- Open the project directly in Unity, or add it to the VCC for easy access and updating later.

## Add UdonSharp to an existing Udon Project:
- Add the project to the VCC, migrating it if necessary.
- Select the project from the Projects listing screen.
- In the Repo dropdown above the Package listings, ensure "Curated" is selected.
![image](/images/repos-official-curated.png)
- Find UdonSharp in the listed packages and press "Add".


## Create or Add UdonSharp with the CLI
[The CLI](https://vcc.docs.vrchat.com/vpm/cli/) is a tool for advanced users, and the best way to manage VPM projects on non-Windows systems for now.
- [New Project from Template](https://vcc.docs.vrchat.com/vpm/cli/#new)
- [Add Package to Project](https://vcc.docs.vrchat.com/vpm/cli/#add-package)

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
