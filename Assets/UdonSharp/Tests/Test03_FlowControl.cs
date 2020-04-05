using UdonSharp;
using UnityEngine;

[AddComponentMenu("")]
public class Test03_FlowControl : UdonSharpBehaviour
{
    public bool shouldLog;
    public bool secondLog;
    public bool thirdLog;
    public bool fourthLog;

    void Start()
    {
        if (shouldLog)
        {
            Debug.Log("Hey I logged!");
        }
        else if (secondLog)
        {
            Debug.Log("The second log");
        }
        else if (thirdLog)
        {
            Debug.Log("The third log");
        }
        else if (fourthLog)
        {
            Debug.Log("The fourth log");
        }
        else
        {
            Debug.Log("The final else");
        }

        int counter = 0;

        while (counter < 10)
        {
            if (counter == 4 || counter > 2)
                break;

            Debug.Log(counter++);
        }

        do
        {
            Debug.Log("Do 1");
        } while (counter == 0); // This will execute once and exit

        counter = 0;

        do
        {
            Debug.Log(counter++);
        } while (counter < 5);

        for (int i = 1; i < 4; ++i)
        {
            for (int j = 1; j < 4; ++j)
            {
                Debug.Log("For loop out: " + (i * j));
            }
        }

        Debug.Log(1 > 0 ? (5f > 7 ? "true float" : "false float") : "false thing");
    }
}
