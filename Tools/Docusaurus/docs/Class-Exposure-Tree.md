# Class Exposure Tree

The Class Exposure Tree will tell you what classes and methods that are available in Udon.

You can open the window by going to `VRChat SDK > Udon Sharp > Class Exposure Tree`

- Red = Not exposed to Udon
- Green = Exposed to Udon

![Udon Type Exposure Tree](/images/type-exposure-tree.png)

The **Show base members** toggle will show methods inherited from base classes that are exposed, for instance on things inheriting from `UnityEngine.Component`, this will show the `GetComponent<T>()` functions since they're defined on the base class.

The **Hide whitelisted accessors** option is mostly there for VRChat if they decide to move away from using the secure heap. This will only show any functions that may return any type of `UnityEngine.Object` through a method return, method parameter, property, or field. This operates recursively so it will catch things that return structs that may contain a `UnityEngine.Object` as well.
