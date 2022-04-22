
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
#pragma warning disable 162

namespace UdonSharp.Tests
{
    enum MySwitchEnum
    {
        A,
        B,
        C,
        D,
        E = 10000000,
        F = 20,
        G,
    }
    
    [AddComponentMenu("Udon Sharp/Tests/SwitchTest")]
    public class SwitchTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            tester.TestAssertion("Switch 1", TestSwitch(1) == "one");
            tester.TestAssertion("Switch 2", TestSwitch(2) == "two or three");
            tester.TestAssertion("Switch 3", TestSwitch(3) == "two or three");
            tester.TestAssertion("Switch 4", TestSwitch(4) == "default");
            tester.TestAssertion("Switch 5", TestSwitch(5) == "it's five");
            tester.TestAssertion("Switch 6", TestSwitch(20) == "twenty");
            tester.TestAssertion("Switch 7", TestSwitch(100000000) == "no jump table");
            
            tester.TestAssertion("User enum switch 1", TestUserEnumSwitch(MySwitchEnum.A) == "A");
            tester.TestAssertion("User enum switch 2", TestUserEnumSwitch(MySwitchEnum.B) == "B");
            tester.TestAssertion("User enum switch 3", TestUserEnumSwitch(MySwitchEnum.C) == "C");
            tester.TestAssertion("User enum switch 4", TestUserEnumSwitch(MySwitchEnum.D) == "C");
            tester.TestAssertion("User enum switch 5", TestUserEnumSwitch(MySwitchEnum.E) == "E");
            
            tester.TestAssertion("Extern enum switch 1", ExternEnumSwitch(CameraClearFlags.Skybox) == "Skybox");
            tester.TestAssertion("Extern enum switch 2", ExternEnumSwitch(CameraClearFlags.Color) == "Color");
            tester.TestAssertion("Extern enum switch 3", ExternEnumSwitch(CameraClearFlags.Depth) == "Default enum handling Depth");
            
            tester.TestAssertion("String switch 1", StringSwitch("testVal") == "the testVal");
            tester.TestAssertion("String switch 2", StringSwitch("testVal2") == "the testVal2");
            tester.TestAssertion("String switch 3", StringSwitch("aaaaa") == "no switch val found");
            tester.TestAssertion("String switch 4", StringSwitch(null) == "null str");
            
            tester.TestAssertion("Float switch 1", FloatSwitch(1) == "one");
            tester.TestAssertion("Float switch 2", FloatSwitch(2) == "two");
            tester.TestAssertion("Float switch 3", FloatSwitch(1.2f) == "no switch val found");
            
            tester.TestAssertion("User enum jump table switch 1", TestUserJumpTableEnumSwitch(MySwitchEnum.A) == "A");
            tester.TestAssertion("User enum jump table switch 2", TestUserJumpTableEnumSwitch(MySwitchEnum.B) == "B");
            tester.TestAssertion("User enum jump table switch 3", TestUserJumpTableEnumSwitch(MySwitchEnum.C) == "C");
            tester.TestAssertion("User enum jump table switch 4", TestUserJumpTableEnumSwitch(MySwitchEnum.D) == "C");
            tester.TestAssertion("User enum jump table switch 5", TestUserJumpTableEnumSwitch(MySwitchEnum.E) == "C");
            tester.TestAssertion("User enum jump table switch 6", TestUserJumpTableEnumSwitch(MySwitchEnum.F) == "f");
            tester.TestAssertion("User enum jump table switch 7", TestUserJumpTableEnumSwitch(MySwitchEnum.G) == "C");
            
            tester.TestAssertion("Object switch 1", ObjectSwitch(null) == "no switch val found");
            tester.TestAssertion("Object switch 2", ObjectSwitch(2) == "two");
            tester.TestAssertion("Object switch 3", ObjectSwitch(2L) == "two long");
            tester.TestAssertion("Object switch 4", ObjectSwitch("testVal") == "the testVal");
            // todo: handle null case on object switches
            // tester.TestAssertion("Object switch 5", ObjectSwitchWithNull(null) == "null");
            // tester.TestAssertion("Object switch 6", ObjectSwitchWithNull(1) == "default");

            tester.TestAssertion("Const variable switch 1", ConstVariableSwitch(1) == "one");
            tester.TestAssertion("Const variable switch 2", ConstVariableSwitch(2) == "two");
            tester.TestAssertion("Const variable switch 3", ConstVariableSwitch(3) == "no switch val found");
            
            tester.TestAssertion("Default case regression 1", TestDefaultCaseRegression(1) == 1);
            tester.TestAssertion("Default case regression 2", TestDefaultCaseRegression(2) == 2);
            tester.TestAssertion("Default case regression 3", TestDefaultCaseRegression(3) == -1);
            
            tester.TestAssertion("Default case fallthrough 1", TestDefaultCaseFallThrough(3) == -1);
            tester.TestAssertion("Default case fallthrough 2", TestDefaultCaseFallThrough(4) == -1);
            tester.TestAssertion("Default case fallthrough 3", TestDefaultCaseFallThrough(1) == 1);
            tester.TestAssertion("Default case fallthrough 4", TestDefaultCaseFallThrough(2) == 2);
        }

        private string TestSwitch(int switchVal)
        {
            switch (switchVal)
            {
                case 1:
                    return "one";
                case 2:
                case 3:
                    return "two or three";
                case 5:
                    break;
                default:
                    return "default";
                case 20:
                    return "twenty";
                case 100000000:
                    return "no jump table";
            }
            
            return "it's five";
        }

        private string TestUserEnumSwitch(MySwitchEnum mySwitchEnum)
        {
            switch (mySwitchEnum)
            {
                case MySwitchEnum.A:
                    return "A";
                case MySwitchEnum.B:
                    return "B";
                default:
                case MySwitchEnum.C:
                    return "C";
                case MySwitchEnum.E:
                    return "E";
            }

            return "This should not be hit";
        }
        
        private string TestUserJumpTableEnumSwitch(MySwitchEnum mySwitchEnum)
        {
            switch (mySwitchEnum)
            {
                case MySwitchEnum.A:
                    return "A";
                case MySwitchEnum.B:
                    return "B";
                default:
                case MySwitchEnum.C:
                    return "C";
                case MySwitchEnum.F:
                    return "f";
            }

            return "This should not be hit";
        }

        private string ExternEnumSwitch(CameraClearFlags clearFlags)
        {
            switch (clearFlags)
            {
                case CameraClearFlags.Color:
                    return "Color";
                case CameraClearFlags.Skybox:
                    return "Skybox";
                default:
                    return $"Default enum handling {clearFlags}";
            }
        }

        private string StringSwitch(string val)
        {
            switch (val)
            {
                case "testVal":
                    return "the testVal";
                case "testVal" + "2":
                    return "the testVal2";
                case null:
                    return "null str";
            }

            return "no switch val found";
        }
        
        private string ObjectSwitch(object val)
        {
            switch (val)
            {
                case "testVal":
                    return "the testVal";
                case 2:
                    return "two";
                case 2L:
                    return "two long";
            }

            return "no switch val found";
        }
        
        private string ObjectSwitchWithNull(object val)
        {
            switch (val)
            {
                case null:
                    return "null";
                case "testVal":
                    return "the testVal";
                case 2:
                    return "two";
                case 2L:
                    return "two long";
                default:
                    return "default";
            }

            return "no switch val found";
        }
        
        private string FloatSwitch(float val)
        {
            switch (val)
            {
                case 1:
                    return "one";
                case 2f:
                    return "two";
            }

            return "no switch val found";
        }
        
        const int one = 1;
        private string ConstVariableSwitch(int val)
        {
            const int two = 2;

            switch (val)
            {
                case one:
                    return "one";
                case two:
                    return "two";
            }

            return "no switch val found";
        }

        // https://github.com/vrchat-community/UdonSharp/issues/26
        private int TestDefaultCaseRegression(int val)
        {
            switch (val)
            {
                default:
                    return -1;
                case 1:
                    return 1;
                case 2:
                    return 2;
            }
        }
        
        private int TestDefaultCaseFallThrough(int val)
        {
            switch (val)
            {
                case 4:
                default:
                case 3:
                    return -1;
                case 1:
                    return 1;
                case 2:
                    return 2;
            }
        }
    }
}
