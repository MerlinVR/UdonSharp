# Random Tips & Performance Pointers

This page contains a list of things to keep in mind while using Udon with U#.

### Udon is slow
Do not expect to do a massive complex algorithm that iterates over thousands of elements. Udon can take on the order of 200x to 1000x longer to run a piece of code than the equivalent in normal C# depending on what you're doing. What this means is that if you're doing anything that iterates on Update, you probably shouldn't. Want to iterate over 40 GameObject's and rotate them? Do it in an animation instead. Just 40 iterations of something can often be enough to visibly impact frame rate on a decent computer. If there's any built-in Unity or VRC component that you could use to do a task you're considering using Udon for, use the component instead. If you must use Udon to do a lot of things at once, time slice your code where it's possible. And you will need to make compromises where it's just not viable to do something.

### Network event scoping
In order to send a CustomNetworkEvent, the target method must be `public`. If it is not public, it will not fire. Additionally, as of VRChat 2020.4.4 any methods that start with an underscore such as `_MyLocalMethod` will not receive network events. Generally for code readability, you should probably just keep any methods you don't want to get called from SendCustomNetworkEvent as `private`. Put an underscore in front of the method name if you want a public method that's callable locally by other UdonSharpBehaviours, but not callable over the network.

### SendCustomEvent & method calls across behaviours
In order to call `SendCustomEvent` on a method on an UdonBehaviour, the method must be marked `public` similar to the network events. Additionally, calls across behaviours will be somewhat slower than local method calls due to how Udon handles SendCustomEvent which is used internally for those cases. When you can, prefer to keep similar behavior in one UdonSharpBehaviour rather than separating it into different behaviours.

Prefer to keep methods as private if they are not being called from other scripts. Due to how Udon searches for methods to call, the fewer public methods you have, the better performance-wise.

### Don't use collision ownership transfer on UdonBehaviours
Collision ownership transfer has been bugged for a long time, and will intermittently cause ownership transfer spam that lags out everyone in the world.

### Avoid using GetComponent\<T\> frequently
This is a general Unity best practice because GetComponent\<T\>() is slow, but it's especially true in Udon's case when you're using GetComponent to get an UdonSharpBehaviour type. This is because UdonSharp needs to insert a loop that checks all UdonBehaviours to make sure they're the correct UdonSharpBehaviour type. When you can, only use GetComponent on Start or on some event that happens infrequently.

### U# script files must be connected to one and only one (1) UdonSharpProgramAsset
UdonSharpProgramAssets are the Udon analogue to 1 script, do not change the script assigned to an UdonSharpProgramAsset after it is in use in a scene or you may experience weird issues.