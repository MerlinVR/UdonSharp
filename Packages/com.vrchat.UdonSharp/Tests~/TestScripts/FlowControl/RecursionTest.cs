
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Random = UnityEngine.Random;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/RecursionTest")]
    public class RecursionTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        [RecursiveMethod]
        int Factorial(int input)
        {
            if (input == 1)
                return 1;

            return input * Factorial(input - 1);
        }

        private static int Partition<T>(T[] arr, int left, int right) where T : IComparable
        {
            T pivot = arr[left];
            while (true)
            {
                while (arr[left].CompareTo(pivot) < 0)
                {
                    left++;
                }
                while (arr[right].CompareTo(pivot) > 0)
                {
                    right--;
                }
                if (left < right)
                {
                    T temp = arr[right];
                    arr[right] = arr[left];
                    arr[left] = temp;
                }
                else
                {
                    return right;
                }
            }
        }

        // Copy paste from https://www.tutorialspoint.com/chash-program-to-perform-quick-sort-using-recursion
        [RecursiveMethod]
        private void QuickSort(int[] arr, int left, int right)
        {
            int pivot;
            if (left < right)
            {
                pivot = Partition(arr, left, right);

                if (pivot > 1)
                    QuickSort(arr, left, pivot - 1);

                if (pivot + 1 < right)
                    QuickSort(arr, pivot + 1, right);
            }

            arr = null; // Just throw a curveball with something that should be handled, but could break stuff if it isn't handled
        }
        
        [RecursiveMethod]
        private static void QuickSortStatic<T>(T[] arr, int left, int right) where T : IComparable
        {
            int pivot;
            if (left < right)
            {
                pivot = Partition(arr, left, right);

                if (pivot > 1)
                    QuickSortStatic(arr, left, pivot - 1);

                if (pivot + 1 < right)
                    QuickSortStatic(arr, pivot + 1, right);
            }

            arr = null; // Just throw a curveball with something that should be handled, but could break stuff if it isn't handled
        }

        // https://www.geeksforgeeks.org/iterative-quick-sort/
        // Just a test for relative performance of using recursive vs iterative
        //void QuickSortIterative(int[] arr, int l, int h)
        //{
        //    int[] stack = new int[h - l + 1];

        //    int top = -1;

        //    stack[++top] = l;
        //    stack[++top] = h;

        //    while (top >= 0)
        //    {
        //        h = stack[top--];
        //        l = stack[top--];

        //        int p = Partition(arr, l, h);

        //        if (p - 1 > l)
        //        {
        //            stack[++top] = l;
        //            stack[++top] = p - 1;
        //        }

        //        if (p + 1 < h)
        //        {
        //            stack[++top] = p + 1;
        //            stack[++top] = h;
        //        }
        //    }
        //}

        int[] InitTestArray(int size)
        {
            int[] testArray = new int[size];

            for (int i = 0; i < testArray.Length; ++i)
                testArray[i] = i;

            return testArray;
        }

        void ShuffleArray(int[] shuffleArray)
        {
            Random.InitState(1337);

            int n = shuffleArray.Length - 1;
            for (int i = 0; i < n; ++i)
            {
                int r = Random.Range(i + 1, n);
                int flipVal = shuffleArray[r];
                shuffleArray[r] = shuffleArray[i];
                shuffleArray[i] = flipVal;
            }
        }

        bool IsSorted(int[] array)
        {
            for (int i = 0; i < array.Length; ++i)
            {
                if (array[i] != i)
                {
                    return false;
                }
            }

            return true;
        }

        [RecursiveMethod]
        public string CombineStrings(int count, string a, string b)
        {
            if (count == 0)
                return "";

            return string.Concat(a, CombineStrings(count - 1, b, a), CombineStrings(count - 1, a, b));
        }

        [RecursiveMethod]
        public string CombineStringsExtern(int count, string a, string b)
        {
            if (count == 0)
                return "";

            RecursionTest self = this;

            //Debug.Log($"count: {count}, a: {a}, b: {b}"/*, a result: {aResult}, b result: {bResult}"*/);

            return string.Concat(a, self.CombineStringsExtern(count - 1, b, a), self.CombineStringsExtern(count - 1, a, b));
        }

        [RecursiveMethod]
        public string CombineStringsParams(int count, string a, string b, string c, string d, string e)
        {
            if (count == 0)
                return "";

            return string.Concat(a, CombineStringsParams(count - 1, e, d, c, b, a), CombineStringsParams(count - 1, a, b, c, d, e), CombineStringsParams(count - 1, a, a, c, d, e), CombineStringsParams(count - 1, a, b, b, e, e), CombineStringsParams(count - 1, a, b, a, d, e));
        }

        [RecursiveMethod]
        public string CombineStringsNested(int count, string a, string b)
        {
            if (count == 0)
                return "";

            return string.Concat(a, CombineStringsNested(count - 1, CombineStringsNested(count - 1, b, a), CombineStringsNested(count - 1, a, b)), CombineStringsNested(count - 1, CombineStringsNested(count - 1, a, b), CombineStringsNested(count - 1, b, a)), "c");
        }

        Transform[] GetChildrenTransforms(Transform parent)
        {
            Transform[] children = new Transform[parent.childCount];

            for (int i = 0; i < children.Length; ++i)
            {
                children[i] = parent.GetChild(i);
            }

            return children;
        }

        [RecursiveMethod]
        int CountChildren(Transform transformToCount)
        {
            int childCount = transformToCount.childCount;

            foreach (Transform child in transformToCount)
                childCount += CountChildren(child);

            return childCount;
        }

        [RecursiveMethod]
        int CountChildrenForeachExpression(Transform transformToCount)
        {
            int childCount = transformToCount.childCount;

            foreach (Transform child in GetChildrenTransforms(transformToCount))
                childCount += CountChildrenForeachExpression(child);

            return childCount;
        }

        [RecursiveMethod]
        int CountChildrenForeachAccessExpression(Transform transformToCount)
        {
            int childCount = transformToCount.childCount;

            foreach (Transform child in transformToCount.gameObject.transform)
                childCount += CountChildrenForeachAccessExpression(child);

            return childCount;
        }

        int externChildCount;

        [RecursiveMethod]
        void CountChildrenExternalCount(Transform transformToCount)
        {
            externChildCount += transformToCount.childCount;

            foreach (Transform child in transformToCount)
                CountChildrenExternalCount(child);
        }

        int customEventCounter = 4;

        [RecursiveMethod]
        public void CustomEventExternRecursion()
        {
            int originalCounter = customEventCounter;

            RecursionTest self = this;

            if (customEventCounter == 1)
                return;

            customEventCounter -= 1;
            self.SendCustomEvent(nameof(CustomEventExternRecursion));

            customEventCounter = originalCounter * customEventCounter;
        }

        [RecursiveMethod]
        public void CustomEventRecursion()
        {
            int originalCounter = customEventCounter;
            
            if (customEventCounter == 1)
                return;

            customEventCounter -= 1;
            SendCustomEvent(nameof(CustomEventRecursion));

            customEventCounter = originalCounter * customEventCounter;
        }

        /// <summary>
        /// Tests if the temp value from the lhs of the short circuit gets stomped by the recursive call which causes the final expression to evaluate an inaccurate value
        /// </summary>
        [RecursiveMethod]
        bool ShortCircuitRecursion(bool a, int count, bool useValue)
        {
            if (count == 0)
                a = false;

            bool result = a == false || !ShortCircuitRecursion(a, count - 1, false);

            if (useValue)
                return result;

            return true;
        }
        
        private int memory;
        [RecursiveMethod]
        private int Sum() => memory == 0 ? 0 : memory-- + Sum();

        [RecursiveMethod] // Just here to test calling out to other types from recursive methods
        public void ExecuteTests()
        {
            tester.TestAssertion("Basic recursion 4!", Factorial(4) == 24);
            tester.TestAssertion("Basic recursion 5!", Factorial(5) == 120);
            tester.TestAssertion("Basic recursion 12!", Factorial(12) == 479001600);
            
            int arraySize = Random.Range(10000, 11000);
            int[] shuffleArray = InitTestArray(arraySize); // Fuzz a little
            
            ShuffleArray(shuffleArray);
            QuickSortStatic(shuffleArray, 0, shuffleArray.Length - 1);
            
            bool sorted = IsSorted(shuffleArray);
            if (!sorted)
                Debug.LogWarning($"Array size that failed {arraySize}");
            
            tester.TestAssertion("Quicksort recursion", sorted);
            
            RecursionTest self = this;
            
            ShuffleArray(shuffleArray);
            self.QuickSort(shuffleArray, 0, shuffleArray.Length - 1);
            
            tester.TestAssertion("Quicksort external call", IsSorted(shuffleArray));
            
            tester.TestAssertion("Function parameter swap recursion", CombineStrings(6, "a", "b") == "abababababababababababababababababababababababababababababababa");
            
            tester.TestAssertion("Function parameter swap recursion external call", self.CombineStringsExtern(6, "a", "b") == "abababababababababababababababababababababababababababababababa");
            tester.TestAssertion("Params array recursion", CombineStringsParams(4, "a", "b", "c", "d", "e") == "aeaeaaaaeaeeeeeaeeeeeaeeeeeaeeeeaeaeeeeaeaaaaaeaaaaaeaaaaaeaaaaaeaeeeeaeaaaaaeaaaaaeaaaaaeaaaaaeaeeeeaeaaaaaeaaaaaeaaaaaeaaaaaeaeeeeaeaaaaaeaaaaaeaaaaaeaaaa");
            tester.TestAssertion("Nested call recursion", CombineStringsNested(3, "a", "b") == "abaccbcccabccacccccbaccbccccccabccacccbaccbcccccabccaccccccc");
            
            tester.TestAssertion("Count children recursively foreach", CountChildren(transform) == 20);
            tester.TestAssertion("Count children recursively foreach expression", CountChildrenForeachExpression(transform) == 20);
            tester.TestAssertion("Count children recursively foreach access", CountChildrenForeachAccessExpression(transform) == 20);
            
            externChildCount = 0;
            CountChildrenExternalCount(transform);
            
            tester.TestAssertion("Count children recursively foreach external counter", externChildCount == 20);
            
            customEventCounter = 4;
            CustomEventExternRecursion();
            
            tester.TestAssertion("SendCustomEvent extern recursion", customEventCounter == 24);
            
            customEventCounter = 4;
            CustomEventRecursion();
            tester.TestAssertion("SendCustomEvent recursion", customEventCounter == 24);
            
            tester.TestAssertion("Recursive short circuit operators", ShortCircuitRecursion(true, 2, true) == false);

            memory = 10;
            tester.TestAssertion("Postfix recursion", Sum() == 55);
        }
    }
}
