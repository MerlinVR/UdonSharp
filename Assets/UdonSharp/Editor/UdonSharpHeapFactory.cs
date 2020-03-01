using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    public class HeapFactory : IUdonHeapFactory
    {
        private uint factoryHeapSize;

        public HeapFactory(uint heapSizeIn)
        {
            factoryHeapSize = heapSizeIn;
        }

        public IUdonHeap ConstructUdonHeap()
        {
            return new UdonHeap(factoryHeapSize);
        }

        public IUdonHeap ConstructUdonHeap(uint heapSize)
        {
            return new UdonHeap(factoryHeapSize);
        }
    }
}
