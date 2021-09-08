
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    internal class HeapFactory : IUdonHeapFactory
    {
        public uint FactoryHeapSize { get; set; }

        public HeapFactory()
        {
            FactoryHeapSize = 0;
        }

        public IUdonHeap ConstructUdonHeap()
        {
            return new UdonHeap(FactoryHeapSize);
        }

        public IUdonHeap ConstructUdonHeap(uint heapSize)
        {
            return new UdonHeap(FactoryHeapSize);
        }
    }
}
