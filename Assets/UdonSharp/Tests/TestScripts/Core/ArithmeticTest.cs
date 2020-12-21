
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
        }

        void IntBinaryOps()
        {
            int result = 4 + 6;
            tester.TestAssertion("Integer Addition", result == 10);

            result = 3 - 5;
            tester.TestAssertion("Integer Subtraction", result == -2);

            result = 20 / 3;
            tester.TestAssertion("Integer Division", result == 6);
            
            result = 5 % 2;
            tester.TestAssertion("Integer Remainder", result == 1);

            result = 2 | 8;
            tester.TestAssertion("Integer OR", result == 10);

            result = 2 & 10;
            tester.TestAssertion("Integer AND", result == 2);

            tester.TestAssertion("Integer Left Shift", 1 << 2 == 4);
            tester.TestAssertion("Integer Right Shift", 4 >> 2 == 1);
            tester.TestAssertion("Integer XOR", (0x499602D3 ^ 0x132C10CB) == 0x5ABA1218); // Randomly chosen numbers

        }

        void SByteBinaryOps()
        {
            // Explicit cast because we don't have constant folding yet
            sbyte result = (sbyte)(4 + 6);
            tester.TestAssertion("sByte Addition", result == 10);

            result = (sbyte)(3 - 5);
            tester.TestAssertion("sByte Subtraction", result == -2);

            result = (sbyte)(20 / 3);
            tester.TestAssertion("sByte Division", result == 6);

            result = (sbyte)(5 % 2);
            tester.TestAssertion("sByte Remainder", result == 1);

            result = (sbyte)((sbyte)2 | 8);
            tester.TestAssertion("sByte OR", result == 10);

            result = (sbyte)((sbyte)2 & 10);
            tester.TestAssertion("sByte AND", result == 2);

            tester.TestAssertion("sByte Left Shift", (sbyte)1 << 2 == 4);
            tester.TestAssertion("sByte Right Shift", (sbyte)4 >> 2 == 1);
            tester.TestAssertion("sByte XOR", ((sbyte)0x03 ^ 0x0B) == 0x08); // Randomly chosen numbers
        }

        void LongBinaryOps() 
        {
            long result = (long)4 + 6;
            tester.TestAssertion("Long Addition", result == 10);

            result = (long)3 - 5; 
            tester.TestAssertion("Long Subtraction", result == -2);

            result = (long)20 / 3;
            tester.TestAssertion("Long Division", result == 6);

            // udonsupport: https://vrchat.canny.io/vrchat-udon-closed-alpha-feedback/p/long-remainder
            //result = (long)5 % 2;
            //tester.TestAssertion("Long Remainder", result == 1);

            result = ((long)2 | 8);
            tester.TestAssertion("Long OR", result == 10);

            result = ((long)2 & 10);
            tester.TestAssertion("Long AND", result == 2);

            tester.TestAssertion("Long Left Shift", (long)1 << 2 == 4);
            tester.TestAssertion("Long Right Shift", (long)4 >> 2 == 1);
            tester.TestAssertion("Long XOR", (0x499602D3499602D3 ^ 0x132C10CB132C10CB) == 0x5ABA12185ABA1218); // Randomly chosen numbers
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
            tester.TestAssertion("Float to Int Truncation", truncatedValue == 4);

            truncatedValue = (int)4.7;
            tester.TestAssertion("Double to Int Truncation", truncatedValue == 4);
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
            //x = (x ^ 3);
            //tester.TestAssertion("uint ^", x == 10);
        }

        void DecimalOps()
        {
            decimal x = 4;

            tester.TestAssertion("Decimal equality", x == 4);
            tester.TestAssertion("Decimal addition", (x + 5) == 9);
            tester.TestAssertion("Decimal mul", (3 * 0.5m) == 1.5m);
        }

        void StringAddition()
        {
            string s = "ab";
            s = s + "cd";
            s += "ef";
            s += string.Format("{0:x2}", 0x42);
            tester.TestAssertion("String addition", s == "abcdef42");
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
        }
    }
}