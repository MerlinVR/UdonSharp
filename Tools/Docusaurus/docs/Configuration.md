# Configuration

All these settings can be found at `Edit > Project Settings > Udon Sharp`

![Udon Sharp Settings](/images/udon-sharp-settings.png)

# Udon Sharp

### Auto compile on modify
Having this enabled will auto compile scripts when a file is modified and saved.

### Compile all script
Compiles all scripts when ever changes are detected to a U# script.

### Compile on focus
Will only compile when the editor gets focused and changes have been made to a script.

### Script template override
You can define your own custom template to be used when creating U# scripts.
This can be done by dragging a script into the `Script template override` field and that will now be used when you create a new U# script.

`<TemplateClassName>` can be used to set the class name based on the file name you give.

**Default Template**
```cs
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class <TemplateClassName> : UdonSharpBehaviour
{
    void Start()
    {
        
    }
}
```

# Debugging

### Debug build
Enables or disabled `Inline Code` and `Listen for client exceptions`

### Inline Code
Includes the C# inline code in the generated assembly code.

### Listen for client exceptions
This will listen for exceptions from the output log the VRChat client makes, then try to match it up against scripts in the project.

[Read more here on how to set it up](https://github.com/vrchat-community/UdonSharp/wiki/class-exposure-tree)