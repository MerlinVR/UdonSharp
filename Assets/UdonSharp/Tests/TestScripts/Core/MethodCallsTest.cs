
using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/MethodCallsTest")]
    public class MethodCallsTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        string[] splitStrs = new string[] { "aaaa", "a b c d", "e f g h i" };

        private string MyParamsMethod(int a, params object[] elements)
        {
            return a + elements[0].ToString();
        }
        
        private string MyParamsMethod(int a, params UnityEngine.Object[] elements)
        {
            return a + elements[0].ToString();
        }

        public void ExecuteTests()
        {
            string testStr = string.Concat("a", "bc", "d", "e", "fg", "hij", "klmn", "opq", "rstuv", "wx", "yz");

            tester.TestAssertion("Params arrays", testStr == "abcdefghijklmnopqrstuvwxyz");
            
            string joinedStr = string.Join(", ", new[] { "Hello", "test", "join" });
            string joinedStr2 = string.Join(", ", "Hello", "test", "join" );
            string joinedStr3 = string.Join(", ", (object[])new GameObject[] { gameObject, gameObject });

            tester.TestAssertion("Param parameter without expanding", joinedStr == "Hello, test, join");
            tester.TestAssertion("Param parameter with expanding", joinedStr2 == "Hello, test, join");
            tester.TestAssertion("Param parameter without expanding, object array", joinedStr3 == "MethodCalls (UnityEngine.GameObject), MethodCalls (UnityEngine.GameObject)");

            int intVal = 20;
            string joinedStr4 = string.Join(", ", intVal.ToString());
            tester.TestAssertion("Param parameter single param", joinedStr4 == "20");
            
            tester.TestAssertion("User method params single parameter", MyParamsMethod(10, intVal.ToString()) == "1020");
            
            tester.TestAssertion("User method params array covariant conversion", MyParamsMethod(10, new [] {gameObject}) == "10MethodCalls (UnityEngine.GameObject)");
            tester.TestAssertion("User method params covariant conversion", MyParamsMethod(10, gameObject) == "10MethodCalls (UnityEngine.GameObject)");

            string formatStr = string.Format("{0}, {1}", this, this);
            tester.TestAssertion("FormatStr 1", formatStr == "MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour)");

            string formatStr2 = string.Format("{0}", this);
            tester.TestAssertion("FormatStr 2", formatStr2 == "MethodCalls (VRC.Udon.UdonBehaviour)");

            var objArr = new object[] { this };
            string formatStr3 = string.Format("{0}", objArr);
            tester.TestAssertion("FormatStr 3", formatStr3 == "MethodCalls (VRC.Udon.UdonBehaviour)");

            string formatStr4 = string.Format("{0}, {1}", new string[] { "a", "b" });
            
            tester.TestAssertion("FormatStr 4", formatStr4 == "a, b");

            tester.TestAssertion("String Join Objects params", string.Join(", ", this, this, this, this) == "MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour)"); 
            tester.TestAssertion("String Join Objects array", string.Join(", ", new object[] { this, this, this, this }) == "MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour)");

            tester.TestAssertion("Split test", "a b c d".Split(new [] { ' ' }, System.StringSplitOptions.None).Length == 4);

            enabled = false;
            tester.TestAssertion("UdonBehaviour enabled", enabled == false);
            enabled = true;

            UdonSharpBehaviour self = this;

            self.enabled = false;
            tester.TestAssertion("UdonSharpBehaviour ref enabled", self.enabled == false);
            self.enabled = true;

            UdonBehaviour selfUdon = (UdonBehaviour)(Component)this;

            selfUdon.enabled = false;
            tester.TestAssertion("UdonBehaviour ref enabled", selfUdon.enabled == false);
            selfUdon.enabled = true;

            string testStr2 = "hello";

            tester.TestAssertion("String indexer", testStr2[0] == 'h' && testStr2[1] == 'e' && testStr2[2] == 'l');

            tester.TestAssertion("Vector2 get indexer", new Vector2(1f, 2f)[1] == 2f);
            tester.TestAssertion("Vector3 get indexer", new Vector3(1f, 2f)[1] == 2f);
            tester.TestAssertion("Vector4 get indexer", new Vector4(1f, 2f)[1] == 2f);
            tester.TestAssertion("Matrix4x4 get indexer", Matrix4x4.identity[0] == 1f && Matrix4x4.identity[1] == 0f);

            Vector2 vec2Test = new Vector2(1f, 2f);
            vec2Test[0] = 4f;
            tester.TestAssertion("Vector2 set indexer", vec2Test[0] == 4f);
            
            Vector3 vec3Test = new Vector3(1f, 2f, 3f);
            vec3Test[0] = 4f;
            tester.TestAssertion("Vector3 set indexer", vec3Test[0] == 4f);
            
            Vector4 vec4Test = new Vector4(1f, 2f, 3f, 4f);
            vec4Test[0] = 4f;
            tester.TestAssertion("Vector4 set indexer", vec4Test[0] == 4f);
            
            Matrix4x4 mat4x4Test = Matrix4x4.identity;
            mat4x4Test[1] = 4f;
            tester.TestAssertion("Matrix4x4 set indexer", mat4x4Test[1] == 4f);
            
            mat4x4Test[0] += 2f;
            tester.TestAssertion("Matrix4x4 get and set in place", mat4x4Test[0] == 3f);

            SphericalHarmonicsL2 harmonicsL2 = new SphericalHarmonicsL2();

            harmonicsL2[0, 1] = 4;
            
            tester.TestAssertion("Multi param indexer operator", harmonicsL2[0, 1] == 4f);

            // tester.TestAssertion("U# Behaviour GetComponent", tester.GetComponent<IntegrationTestSuite>() != null);
            // tester.TestAssertion("UdonBehaviour GetComponent", ((UdonBehaviour)(Component)tester).GetComponent<IntegrationTestSuite>() != null);
            //
            RigidbodyConstraints constraints = (RigidbodyConstraints)126;
            
            tester.TestAssertion("Enum cast", constraints == RigidbodyConstraints.FreezeAll);
            
            constraints = RigidbodyConstraints.FreezePosition;
            
            tester.TestAssertion("Enum assignment after cast", constraints == RigidbodyConstraints.FreezePosition);
            tester.TestAssertion("Enum type after cast", (RigidbodyConstraints)126 == RigidbodyConstraints.FreezeAll);

            Transform currentParent = transform.parent;

            transform.SetParent(null);

            tester.TestAssertion("Transform detach parent (null parameter method finding)", transform.parent == null);

            transform.SetParent(currentParent);

            selfUdon.DisableInteractive = true;

            tester.TestAssertion("DisableInteractive true", selfUdon.DisableInteractive); 
            
            self.DisableInteractive = false;

            tester.TestAssertion("DisableInteractive false", !self.DisableInteractive);

            DisableInteractive = true;

            tester.TestAssertion("DisableInteractive true 2", DisableInteractive);

            DisableInteractive = false;
            tester.TestAssertion("DisableInteractive false 2", !DisableInteractive);

            VRCPlayerApi player = null;

            if (Utilities.IsValid(player))
            {
                player.GetPickupInHand(VRC_Pickup.PickupHand.Left);
            }
        }
    }
}
