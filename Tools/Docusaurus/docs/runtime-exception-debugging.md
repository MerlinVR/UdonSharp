# Runtime Exception Debugging

You will often find yourself with programs that can only be debugged in-game. In order to catch these errors and make them easier to understand, U# includes a runtime exception watcher that will look for exceptions from VRChat's output log. The watcher will then output the script and line that threw that exception to your editor's log.

### Setup instructions
#### Editor setup
1. In your editor open the project settings menu 

![Project Settings](/images/red-1.png)

2. Enable `Listen for client exceptions` in the Udon Sharp settings 

![Listen for Client Exceptions](/images/red-2.png)

#### VRChat client setup
1. Right-click on VRChat in your Steam library and click `Properties...`

![Steam VRChat Properties](/images/red-3.png)

2. Click `Set Launch Options...`

![Set Launch Options](/images/red-4.png)

3. Add the launch argument `--enable-udon-debug-logging` to the launch options and click OK

![Steam Launch Args](/images/red-5.png)


***

Now once you have the client and editor setup, you just need to start your game and load into the world. Any errors that are thrown in your world will be output to your editor's console. This is an example of what the error will look like, you probably won't be getting the same error and it won't be in the same code file.

![Error in Console](/images/red-6.png)