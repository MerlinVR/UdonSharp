
using System;
using UnityEngine;

#if !COMPILER_UDONSHARP
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("UdonSharp.Editor")]
#endif

namespace UdonSharp.Lib.Internal.Collections
{
    // Goes from top to bottom
    internal class StackIterator<T>
    {
        private Stack<T> _stack;
        private int _index;
        
        public StackIterator(Stack<T> stack)
        {
            _stack = stack;
            _index = stack._size;
        }
        
        public bool MoveNext()
        {
            return --_index >= 0;
        }
        
        public T Current => _stack._items[_index];
    }
    
    internal class Stack<T>
    {
        internal T[] _items = new T[8];
        internal int _size;
        
        public int Count => _size;
        
        public void Push(T item)
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (size == itemArr.Length)
            {
                T[] newItems = new T[itemArr.Length * 2];
                System.Array.Copy(itemArr, newItems, itemArr.Length);
                _items = newItems;
                itemArr = newItems;
            }
            
            itemArr[size] = item;
            _size = size + 1;
        }
        
        public T Pop()
        {
            int size = _size;
            
            if (size == 0)
            {
                Debug.LogError("Stack is empty");
            #pragma warning disable CS0251
                (new object[0])[-1] = 0; // Crash
            #pragma warning disable CS0251
                return default;
            }

            size -= 1;
            _size = size;
            return _items[size];
        }
        
        public T Peek()
        {
            int size = _size;
            
            if (size == 0)
            {
                Debug.LogError("Stack is empty");
            #pragma warning disable CS0251
                (new object[0])[-1] = 0; // Crash
            #pragma warning disable CS0251
                return default;
            }

            return _items[size - 1];
        }
        
        public T[] ToArray()
        {
            T[] itemArr = new T[_size];
            System.Array.Copy(_items, itemArr, _size);
            return itemArr;
        }
        
        public void TrimExcess()
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (size < itemArr.Length * 0.9)
            {
                T[] newItems = new T[size];
                System.Array.Copy(itemArr, newItems, size);
                _items = newItems;
            }
        }
        
        public bool Contains(T item)
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (item == null)
            {
                for (int i = 0; i < size; i++)
                {
                    if (itemArr[i] == null)
                        return true;
                }
                
                return false;
            }
            
            for (int i = 0; i < size; i++)
            {
                if (item.Equals(itemArr[i]))
                    return true;
            }
            
            return false;
        }
        
        public bool TryPeek(out T item)
        {
            int size = _size;
            
            if (size == 0)
            {
                item = default;
                return false;
            }

            item = _items[size - 1];
            return true;
        }
        
        public bool TryPop(out T item)
        {
            int size = _size;
            
            if (size == 0)
            {
                item = default;
                return false;
            }

            _size = size - 1;
            item = _items[size];
            return true;
        }
        
        public void Clear()
        {
            _size = 0;
            Array.Clear(_items, 0, _items.Length);
        }
        
        public StackIterator<T> GetEnumerator()
        {
            return new StackIterator<T>(this);
        }
    }
}