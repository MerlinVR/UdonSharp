
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/ArithmeticTest")]
    public class ArithmeticTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            IntBinaryOps();
            SByteBinaryOps();
            LongBinaryOps();
            IntIncrement();
            UIntIncrement();
            IntAssignment();
            LongAssignment();
            ByteIncrement();
            ByteAssignment();
            LongIncrement();
            ShortIncrement();
            UShortIncrement();
            IntTruncate();
            UintBitOps();
            StringAddition();
            DecimalOps();
            BitwiseNot();
            UdonBehaviourFieldCompoundAssignment();
            NullEquals();
            CastCharToFloat();
        }

        void IntBinaryOps()
        {
            int result;
            result = 4 + 6;
            tester.TestAssertion("Constant Integer Addition", result == 10);
        
            result = 3 - 5;
            tester.TestAssertion("Constant Integer Subtraction", result == -2);
        
            result = 20 / 3;
            tester.TestAssertion("Constant Integer Division", result == 6);
            
            result = 5 % 2;
            tester.TestAssertion("Constant Integer Remainder", result == 1);
        
            result = 2 | 8;
            tester.TestAssertion("Constant Integer OR", result == 10);
        
            result = 2 & 10;
            tester.TestAssertion("Constant Integer AND", result == 2);
        
            tester.TestAssertion("Constant Integer Left Shift", 1 << 2 == 4);
            tester.TestAssertion("Constant Integer Right Shift", 4 >> 2 == 1);
            tester.TestAssertion("Constant Integer XOR", (0x499602D3 ^ 0x132C10CB) == 0x5ABA1218); // Randomly chosen numbers

            int a = 13;
            int b = 2;
            result = a + b;
            tester.TestAssertion("Integer Addition", result == 15);
        
            result = b - a;
            tester.TestAssertion("Integer Subtraction", result == -11);
        
            result = a / b;
            tester.TestAssertion("Integer Division", result == 6);
            
            result = a % b;
            tester.TestAssertion("Integer Remainder", result == 1);
        
            result = a | b;
            tester.TestAssertion("Integer OR", result == 15);
        
            result = a & b;
            tester.TestAssertion("Integer AND", result == 0);
        
            tester.TestAssertion("Integer Left Shift", a << b == 52);
            tester.TestAssertion("Integer Right Shift", a >> b == 3);
            tester.TestAssertion("Integer XOR", (a ^ b) == 15);
        
        }
        
        void SByteBinaryOps()
        {
            sbyte result = (sbyte)(4 + 6);
            tester.TestAssertion("Constant sByte Addition", result == 10);
        
            result = 3 - 5;
            tester.TestAssertion("Constant sByte Subtraction", result == -2);
        
            result = 20 / 3;
            tester.TestAssertion("Constant sByte Division", result == 6);
        
            result = 5 % 2;
            tester.TestAssertion("Constant sByte Remainder", result == 1);
        
            result = 2 | 8;
            tester.TestAssertion("Constant sByte OR", result == 10);
        
            result = 2 & 10;
            tester.TestAssertion("Constant sByte AND", result == 2);
        
            tester.TestAssertion("Constant sByte Left Shift", (sbyte)1 << 2 == 4);
            tester.TestAssertion("Constant sByte Right Shift", (sbyte)4 >> 2 == 1);
            tester.TestAssertion("Constant sByte XOR", ((sbyte)0x03 ^ 0x0B) == 0x08); // Randomly chosen numbers
            
            sbyte a = 13;
            sbyte b = 2;
            result = (sbyte)(a + b);
            tester.TestAssertion("sByte Addition", result == 15);
        
            result = (sbyte)(b - a);
            tester.TestAssertion("sByte Subtraction", result == -11);
        
            result = (sbyte)(a / b);
            tester.TestAssertion("sByte Division", result == 6);
            
            result = (sbyte)(a % b);
            tester.TestAssertion("sByte Remainder", result == 1);
        
            result = (sbyte)(a | b);
            tester.TestAssertion("sByte OR", result == 15);
        
            result = (sbyte)(a & b);
            tester.TestAssertion("sByte AND", result == 0);
        
            tester.TestAssertion("sByte Left Shift", a << b == 52);
            tester.TestAssertion("sByte Right Shift", a >> b == 3);
            tester.TestAssertion("sByte XOR", (a ^ b) == 15);
        }
        
        void LongBinaryOps() 
        {
            long result = 4L + 6L;
            tester.TestAssertion("Const Long Addition", result == 10);
        
            result = 3L - 5L; 
            tester.TestAssertion("Const Long Subtraction", result == -2);
        
            result = 20L / 3L;
            tester.TestAssertion("Const Long Division", result == 6);
        
            result = 5L % 2L;
            tester.TestAssertion("Const Long Remainder", result == 1);
        
            result = 2L | 8L;
            tester.TestAssertion("Const Long OR", result == 10);
        
            result = 2L & 10L;
            tester.TestAssertion("Const Long AND", result == 2);
        
            tester.TestAssertion("Const Long Left Shift", (long)1 << 2 == 4);
            tester.TestAssertion("Const Long Right Shift", (long)4 >> 2 == 1);
            tester.TestAssertion("Const Long XOR", (0x499602D3499602D3 ^ 0x132C10CB132C10CB) == 0x5ABA12185ABA1218); // Randomly chosen numbers

            long a = 13;
            long b = 2;
            result = a + b;
            tester.TestAssertion("Long Addition", result == 15);
        
            result = b - a;
            tester.TestAssertion("Long Subtraction", result == -11);
        
            result = a / b;
            tester.TestAssertion("Long Division", result == 6);
            
            // udonsupport: https://vrchat.canny.io/vrchat-udon-closed-alpha-feedback/p/long-remainder
            // result = a % b;
            // tester.TestAssertion("Long Remainder", result == 1);
        
            result = a | b;
            tester.TestAssertion("Long OR", result == 15);
        
            result = a & b;
            tester.TestAssertion("Long AND", result == 0);
        
            tester.TestAssertion("Long Left Shift", a << (int)b == 52);
            tester.TestAssertion("Long Right Shift", a >> (int)b == 3);
            tester.TestAssertion("Long XOR", (a ^ b) == 15);
        }
        
        void IntIncrement()
        {
            int testVal = 4;
        
            tester.TestAssertion("Integer Prefix Increment", ++testVal == 5);
            tester.TestAssertion("Integer Postfix Increment", testVal++ == 5);
            tester.TestAssertion("Integer Postfix Increment 2", testVal == 6);
            tester.TestAssertion("Integer Prefix Decrement", --testVal == 5);
            tester.TestAssertion("Integer Postfix Decrement", testVal-- == 5);
            tester.TestAssertion("Integer Postfix Decrement 2", testVal == 4);
        
            // Also test increment/decrements without consuming the value;
            testVal++;
            tester.TestAssertion("Integer Prefix Increment (out of line)", testVal == 5);
            ++testVal;
            tester.TestAssertion("Integer Prefix Increment (out of line)", testVal == 6);
            --testVal;
            tester.TestAssertion("Integer Prefix Decrement (out of line)", testVal == 5);
            --testVal;
            tester.TestAssertion("Integer Prefix Decrement (out of line)", testVal == 4);
        
            tester.TestAssertion("Integer +=", (testVal += 3) == 7);
        
            testVal += 4;
            tester.TestAssertion("Integer += (out of line)", testVal == 11);
        
            tester.TestAssertion("Integer -=", (testVal -= 2) == 9);
        
            testVal -= 7;
            tester.TestAssertion("Integer += (out of line)", testVal == 2);
        }
        
        void UIntIncrement()
        {
            uint testVal = 4;
        
            tester.TestAssertion("Unsigned Integer Prefix Increment", ++testVal == 5);
            tester.TestAssertion("Unsigned Integer Postfix Increment", testVal++ == 5);
            tester.TestAssertion("Unsigned Integer Postfix Increment 2", testVal == 6);
            tester.TestAssertion("Unsigned Integer Prefix Decrement", --testVal == 5);
            tester.TestAssertion("Unsigned Integer Postfix Decrement", testVal-- == 5);
            tester.TestAssertion("Unsigned Integer Postfix Decrement 2", testVal == 4);
        
            testVal = 0;
            tester.TestAssertion("UInt overflow", (testVal - 1u) == uint.MaxValue);
        }
        
        void IntAssignment()
        {
            int testVal = 5;
        
            tester.TestAssertion("Integer Add Assign", (testVal += 4) == 9);
            tester.TestAssertion("Integer Subtract Assign", (testVal -= 20) == -11);
            tester.TestAssertion("Integer Multiply Assign", (testVal *= 8) == -88);
            tester.TestAssertion("Integer Divide Assign", (testVal /= 8) == -11);
        
            testVal = -testVal;
            tester.TestAssertion("Integer Unary Negation", testVal == 11);
        
            tester.TestAssertion("Integer Remainder Assign", (testVal %= 5) == 1);
            tester.TestAssertion("Integer OR Assign", (testVal |= 2) == 3);
            tester.TestAssertion("Integer AND Assign", (testVal &= 1) == 1);
        }
        
        void ByteAssignment()
        {
            sbyte testVal = 5;
            
            tester.TestAssertion("sByte Add Assign", (testVal += 4) == 9);
            tester.TestAssertion("sByte Subtract Assign", (testVal -= 20) == -11);
            tester.TestAssertion("sByte Multiply Assign", (testVal *= 8) == -88);
            tester.TestAssertion("sByte Divide Assign", (testVal /= 8) == -11);
            
            testVal = (sbyte)-testVal;
            tester.TestAssertion("sByte Unary Negation", testVal == 11);
            
            // udonsupport: https://vrchat.canny.io/vrchat-udon-closed-alpha-feedback/p/long-remainder
            //tester.TestAssertion("sByte Remainder Assign", (testVal %= 5) == 1);
            testVal = 1;
            tester.TestAssertion("sByte OR Assign", (testVal |= 2) == 3);
            tester.TestAssertion("sByte AND Assign", (testVal &= 1) == 1);
        }
        
        void LongAssignment()
        {
            long testVal = 5;
        
            tester.TestAssertion("Long Add Assign", (testVal += 4) == 9);
            tester.TestAssertion("Long Subtract Assign", (testVal -= 20) == -11);
            tester.TestAssertion("Long Multiply Assign", (testVal *= 8) == -88);
            tester.TestAssertion("Long Divide Assign", (testVal /= 8) == -11);
        
            testVal = -testVal;
            tester.TestAssertion("Long Unary Negation", testVal == 11);
            
            // udonsupport: https://vrchat.canny.io/vrchat-udon-closed-alpha-feedback/p/long-remainder
            //tester.TestAssertion("Long Remainder Assign", (testVal %= 5) == 1);
            testVal = 1;
            tester.TestAssertion("Long OR Assign", (testVal |= 2) == 3);
            tester.TestAssertion("Long AND Assign", (testVal &= 1) == 1);
        }
        
        void ByteIncrement()
        {
            byte testVal = 4;
        
            tester.TestAssertion("Byte Prefix Increment", ++testVal == 5);
            tester.TestAssertion("Byte Postfix Increment", testVal++ == 5);
            tester.TestAssertion("Byte Postfix Increment 2", testVal == 6);
            tester.TestAssertion("Byte Prefix Decrement", --testVal == 5);
            tester.TestAssertion("Byte Postfix Decrement", testVal-- == 5);
            tester.TestAssertion("Byte Postfix Decrement 2", testVal == 4);
        }
        
        void LongIncrement()
        {
            long testVal = 4;
        
            tester.TestAssertion("Long Prefix Increment", ++testVal == 5);
            tester.TestAssertion("Long Postfix Increment", testVal++ == 5);
            tester.TestAssertion("Long Postfix Increment 2", testVal == 6);
            tester.TestAssertion("Long Prefix Decrement", --testVal == 5);
            tester.TestAssertion("Long Postfix Decrement", testVal-- == 5);
            tester.TestAssertion("Long Postfix Decrement 2", testVal == 4);
        }
        
        void ShortIncrement()
        {
            short testVal = 4;
        
            tester.TestAssertion("Short Prefix Increment", ++testVal == 5);
            tester.TestAssertion("Short Postfix Increment", testVal++ == 5);
            tester.TestAssertion("Short Postfix Increment 2", testVal == 6);
            tester.TestAssertion("Short Prefix Decrement", --testVal == 5);
            tester.TestAssertion("Short Postfix Decrement", testVal-- == 5);
            tester.TestAssertion("Short Postfix Decrement 2", testVal == 4);
        }
        
        void UShortIncrement()
        {
            ushort testVal = 4;
        
            tester.TestAssertion("UShort Prefix Increment", ++testVal == 5);
            tester.TestAssertion("UShort Postfix Increment", testVal++ == 5);
            tester.TestAssertion("UShort Postfix Increment 2", testVal == 6);
            tester.TestAssertion("UShort Prefix Decrement", --testVal == 5);
            tester.TestAssertion("UShort Postfix Decrement", testVal-- == 5);
            tester.TestAssertion("UShort Postfix Decrement 2", testVal == 4);
        }
        
        void IntTruncate()
        {
            int truncatedValue = (int)4.7f;
            tester.TestAssertion("Const Float to Int Truncation", truncatedValue == 4);
        
            truncatedValue = (int)4.7;
            tester.TestAssertion("Const Double to Int Truncation", truncatedValue == 4);

            float testFloat = 4.7f;
            double testDouble = 4.7f;
            truncatedValue = (int)testFloat;
            tester.TestAssertion("Float to Int Truncation", truncatedValue == 4);

            truncatedValue = (int)testDouble;
            tester.TestAssertion("Double to Int Truncation", truncatedValue == 4);

            float testNegativeFloat = -4.7f;
            double testNegativeDouble = -4.7;
            decimal testNegativeDecimal = -4.7m;

            truncatedValue = (int)testNegativeFloat;
            tester.TestAssertion("Negative Float to Int Truncation", truncatedValue == -4);

            truncatedValue = (int)testNegativeDouble;
            tester.TestAssertion("Negative Double to Int Truncation", truncatedValue == -4);

            truncatedValue = (int)testNegativeDecimal;
            tester.TestAssertion("Negative Decimal to Int Truncation", truncatedValue == -4);
        }
        
        void UintBitOps()
        {
            uint x = 1;
            x <<= 1;
            tester.TestAssertion("uint <<=", x == 2);
            x = (x << 2);
            tester.TestAssertion("uint <<", x == 8);
        
            x ^= 1;
            tester.TestAssertion("uint ^=", x == 9);
            // https://github.com/Merlin-san/UdonSharp/issues/23
            x = (x ^ 3);
            tester.TestAssertion("uint ^", x == 10);
        }
        
        void DecimalOps()
        {
            decimal x = 4;
        
            tester.TestAssertion("Decimal equality", x == 4);
            tester.TestAssertion("Decimal addition", (x + 5) == 9);
            tester.TestAssertion("Decimal mul", (x * 0.5m) == 2m);
        }

        private char stringChar = 'b';
        
        void StringAddition()
        {
            string s = "ab";
            s = s + "cd";
            s += "ef";
            s += string.Format("{0:x2}", 0x42);
            tester.TestAssertion("String addition", s == "abcdef42");

            s += 'a';
            tester.TestAssertion("Char addition", s == "abcdef42a");
            
            s += stringChar;
            tester.TestAssertion("Char addition non const", s == "abcdef42ab");
            
            s += 1;
            tester.TestAssertion("Int addition", s == "abcdef42ab1");

            s = "ab";
            s += gameObject;
            tester.TestAssertion("Object addition", s == "ab" + gameObject.ToString());
            
            object o = "ab";
            o = o + "cd";
            o += "ef";
            tester.TestAssertion("String Op +(object, string)", o.ToString() == "abcdef");
        }
        
        void BitwiseNot()
        {
            int positiveInt = 30;
        
            int resultInt = ~positiveInt;
            tester.TestAssertion("Int bitwise not positive", resultInt == -31);
            tester.TestAssertion("Int bitwise not positive 2", ~positiveInt == -31);
            
            int negativeInt = -30;
        
            resultInt = ~negativeInt;
            tester.TestAssertion("Int bitwise not negative", resultInt == 29);
            tester.TestAssertion("Int bitwise not negative 2", ~negativeInt == 29);
        
            uint uintTest = 40;
            uint resultUint = ~uintTest;
            tester.TestAssertion("uint bitwise not", ~uintTest == 4294967255);
            tester.TestAssertion("uint bitwise not 2", resultUint == 4294967255);

            long la = (long)0x1111111111111111;
            long lb = (long)~la;

            tester.TestAssertion("Long bitwise NOT 1", lb == -0x1111111111111112);
            tester.TestAssertion("Long bitwise NOT 2", ~lb == la);
        }

        private float _testFloat = 2;
        private Vector3 _testVec = new Vector3(1, 2, 3);
        
        void UdonBehaviourFieldCompoundAssignment()
        {
            ArithmeticTest self = this;

            self._testFloat += 2f;
            
            tester.TestAssertion("Field compound assignment", _testFloat == 4);

            self._testVec.x += 3;
            
            tester.TestAssertion("Field struct compound assignment", _testVec.x == 4);
        }
        
        void NullEquals()
        {
            tester.TestAssertion("Null equals null", null == null);
            tester.TestAssertion("Null doesn't equal null", !(null != null));
        }
        
        void CastCharToFloat()
        {
            float af = 97.5f;
            double ad = 98.5;
            decimal am = 99.5m;
            tester.TestAssertion("Cast float to char", (char)af == 'a');
            tester.TestAssertion("Cast double to char", (char)ad == 'b');
            tester.TestAssertion("Cast decimal to char", (char)am == 'c');

            char c = 'a';
            tester.TestAssertion("Cast char to float", c == 97f);
            tester.TestAssertion("Cast char to double", c == 97.0);
            tester.TestAssertion("Cast char to decimal", c == 97m);
        }
    }
}