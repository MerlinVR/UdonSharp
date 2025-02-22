using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if !COMPILER_UDONSHARP
[assembly:InternalsVisibleTo("UdonSharp.Editor")]
#endif

namespace UdonSharp.Lib.Internal.Collections
{
    internal class DictionaryKeyValue<TKey, TValue>
    {
        public TKey key;
        public TValue value;
        
        public TKey Key => key;
        public TValue Value => value;

        // public override string ToString()
        // {
        //     return $"[{key}, {value}]";
        // }
    }
    
    internal class DictionaryIterator<TKey, TValue>
    {
        private DictionaryEntry<TKey, TValue>[] _buckets;
        private DictionaryEntry<TKey, TValue> _entry;
        private int _bucketIndex;
        private DictionaryKeyValue<TKey, TValue> _current;

        public DictionaryIterator(DictionaryEntry<TKey, TValue>[] buckets)
        {
            _buckets = buckets;
            _bucketIndex = 0;
            _entry = null;
            _current = new DictionaryKeyValue<TKey, TValue>();
        }

        public bool MoveNext()
        {
            DictionaryEntry<TKey, TValue> entry = _entry;
            if (entry != null && entry.next != null)
            {
                _entry = entry.next;
                _current.key = _entry.key;
                _current.value = _entry.value;
                return true;
            }
            
            int bucketIndex = _bucketIndex;
            DictionaryEntry<TKey, TValue>[] buckets = _buckets;
            int bucketCount = buckets.Length;

            while (bucketIndex < bucketCount)
            {
                _entry = buckets[bucketIndex++];

                if (_entry != null)
                {
                    _bucketIndex = bucketIndex;
                    _current.key = _entry.key;
                    _current.value = _entry.value;
                    return true;
                }
            }

            return false;
        }

        public DictionaryKeyValue<TKey, TValue> Current => _current;
    }
    
    internal class DictionaryEntry<TKey, TValue>
    {
        public TKey key;
        public TValue value;
        public DictionaryEntry<TKey, TValue> next;

        public DictionaryEntry(TKey key, TValue value)
        {
            // object[] thisArray = (object[])(object)this;
            // string internalValueStr = "";
            // foreach (object obj in thisArray)
            // {
            //     internalValueStr += $"{(obj != null ? obj.ToString() : "null")}, ";
            // }
            //
            // Debug.Log($"Setting key: ({typeof(TKey)}){key}, value: ({typeof(TValue)}){value}, hashCode: {hashCode}, internal values: ({internalValueStr})");
            
            this.key = key;
            this.value = value;
        }
    }
    
    internal class SerializedDictionaryData<TKey, TValue>
    {
        public TKey[] keys;
        public TValue[] values;
        
        public SerializedDictionaryData(TKey[] keys, TValue[] values)
        {
            this.keys = keys;
            this.values = values;
        }
    }

    internal class Dictionary<TKey, TValue>
    {
        private const int InitialSize = 23; // Prime number for better distribution initially
        private const float LoadFactor = 0.75f;

        private DictionaryEntry<TKey, TValue>[] _buckets = new DictionaryEntry<TKey, TValue>[InitialSize];
        private int _size;
        private int _threshold = (int)(InitialSize * LoadFactor);
        private DictionaryEntry<TKey, TValue>[] _freeList = new DictionaryEntry<TKey, TValue>[8];
        private int _freeCount;
        
        private SerializedDictionaryData<TKey, TValue> _serializedData;
        
        public int Count => _size;

        public void Add(TKey key, TValue value)
        {
            if (key == null)
            {
                Debug.LogError("Key cannot be null.");
            #pragma warning disable CS0251
                (new object[0])[-1] = 42; // Crash
            #pragma warning disable CS0251
            }
            
            if (_serializedData != null)
                LoadSerializedData();
            
            int size = _size;
            if (size >= _threshold)
                Resize();

            // ReSharper disable once PossibleNullReferenceException
            int hashCode = key.GetHashCode();
            DictionaryEntry<TKey, TValue>[] buckets = _buckets;
            int bucketIndex = Mathf.Abs(hashCode % buckets.Length);

            DictionaryEntry<TKey, TValue> entry = buckets[bucketIndex];

            if (entry == null)
            {
                buckets[bucketIndex] = GetFreeEntry(key, value);
            }
            else
            {
                while (true)
                {
                    if (entry.key.Equals(key))
                    {
                        Debug.LogError("An item with the same key has already been added.");
                    #pragma warning disable CS0251
                        (new object[0])[-1] = 42; // Crash
                    #pragma warning disable CS0251
                        // throw new System.ArgumentException("An item with the same key has already been added.");
                    }

                    if (entry.next == null)
                    {
                        entry.next = GetFreeEntry(key, value);
                        break;
                    }

                    entry = entry.next;
                }
            }

            _size = size + 1;
        }
        
        public TValue this[TKey key]
        {
            get
            {
                if (key == null)
                {
                    Debug.LogError("Key cannot be null.");
                #pragma warning disable CS0251
                    (new object[0])[-1] = 42; // Crash
                #pragma warning disable CS0251
                }
                
                if (_serializedData != null)
                    LoadSerializedData();

                // ReSharper disable once PossibleNullReferenceException
                int hashCode = key.GetHashCode();
                DictionaryEntry<TKey, TValue>[] buckets = _buckets;
                int bucketIndex = Mathf.Abs(hashCode % buckets.Length);

                DictionaryEntry<TKey, TValue> entry = buckets[bucketIndex];

                while (entry != null)
                {
                    if (entry.key.Equals(key))
                        return entry.value;

                    entry = entry.next;
                }

                Debug.LogError("Key not found.");
            #pragma warning disable CS0251
                (new object[0])[-1] = 42; // Crash
            #pragma warning disable CS0251
                return default(TValue);
            }
            set
            {
                if (key == null)
                {
                    Debug.LogError("Key cannot be null.");
                #pragma warning disable CS0251
                    (new object[0])[-1] = 42; // Crash
                #pragma warning disable CS0251
                }
                
                if (_serializedData != null)
                    LoadSerializedData();
                
                // ReSharper disable once PossibleNullReferenceException
                int hashCode = key.GetHashCode();
                DictionaryEntry<TKey, TValue>[] buckets = _buckets;
                int bucketIndex = Mathf.Abs(hashCode % buckets.Length);

                DictionaryEntry<TKey, TValue> entry = buckets[bucketIndex];
                
                // Insert new entry if no entries exist
                if (entry == null)
                {
                    _buckets[bucketIndex] = GetFreeEntry(key, value);
                    _size++;
                    
                    if (_size >= _threshold)
                        Resize();
                    
                    return;
                }
                
                // Check if the key already exists
                while (true)
                {
                    if (entry.key.Equals(key))
                    {
                        entry.value = value;
                        return;
                    }

                    if (entry.next == null)
                        break;

                    entry = entry.next;
                }
                
                // Add new entry to the end of the chain
                entry.next = GetFreeEntry(key, value);
                _size++;
                
                if (_size >= _threshold)
                    Resize();
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_serializedData != null)
                LoadSerializedData();

            int hashCode = key.GetHashCode();
            var buckets = _buckets;
            int bucketIndex = Mathf.Abs(hashCode % buckets.Length);

            DictionaryEntry<TKey, TValue> entry = buckets[bucketIndex];

            while (entry != null)
            {
                if (entry.key.Equals(key))
                {
                    value = entry.value;
                    return true;
                }

                entry = entry.next;
            }

            value = default(TValue);
            return false;
        }
        
        public bool Remove(TKey key)
        {
            if (key == null)
            {
                Debug.LogError("Key cannot be null.");
            #pragma warning disable CS0251
                (new object[0])[-1] = 42; // Crash
            #pragma warning disable CS0251
            }
            
            if (_serializedData != null)
                LoadSerializedData();
            
            // ReSharper disable once PossibleNullReferenceException
            int hashCode = key.GetHashCode();
            DictionaryEntry<TKey, TValue>[] buckets = _buckets;
            int bucketIndex = Mathf.Abs(hashCode % buckets.Length);

            DictionaryEntry<TKey, TValue> entry = buckets[bucketIndex];
            DictionaryEntry<TKey, TValue> previousEntry = null;

            while (entry != null)
            {
                if (entry.key.Equals(key))
                {
                    if (previousEntry == null)
                        buckets[bucketIndex] = entry.next;
                    else
                        previousEntry.next = entry.next;
                    
                    ReturnFreeEntry(entry);

                    _size--;
                    return true;
                }

                previousEntry = entry;
                entry = entry.next;
            }

            return false;
        }

        public bool Remove(TKey key, out TValue value)
        {
            if (key == null)
            {
                Debug.LogError("Key cannot be null.");
            #pragma warning disable CS0251
                (new object[0])[-1] = 42; // Crash
            #pragma warning disable CS0251
            }
            
            // ReSharper disable once PossibleNullReferenceException
            int hashCode = key.GetHashCode();
            
            DictionaryEntry<TKey, TValue>[] buckets = _buckets;
            int bucketIndex = Mathf.Abs(hashCode % buckets.Length);

            DictionaryEntry<TKey, TValue> entry = buckets[bucketIndex];
            DictionaryEntry<TKey, TValue> previousEntry = null;

            while (entry != null)
            {
                if (entry.key.Equals(key))
                {
                    if (previousEntry == null)
                        buckets[bucketIndex] = entry.next;
                    else
                        previousEntry.next = entry.next;

                    _size--;
                    value = entry.value;
                    return true;
                }

                previousEntry = entry;
                entry = entry.next;
            }

            value = default(TValue);
            return false;
        }
        
        public bool ContainsKey(TKey key)
        {
            if (key == null)
            {
                Debug.LogError("Key cannot be null.");
            #pragma warning disable CS0251
                (new object[0])[-1] = 42; // Crash
            #pragma warning disable CS0251
            }
            
            if (_serializedData != null)
                LoadSerializedData();

            // ReSharper disable once PossibleNullReferenceException
            int hashCode = key.GetHashCode();
            DictionaryEntry<TKey, TValue>[] buckets = _buckets;
            int bucketIndex = Mathf.Abs(hashCode % buckets.Length);

            DictionaryEntry<TKey, TValue> entry = buckets[bucketIndex];

            while (entry != null)
            {
                if (entry.key.Equals(key))
                    return true;

                entry = entry.next;
            }

            return false;
        }
        
        public bool ContainsValue(TValue value)
        {
            if (_serializedData != null)
                LoadSerializedData();
            
            DictionaryEntry<TKey, TValue>[] buckets = _buckets;
            int bucketCount = buckets.Length;
            
            for (int i = 0; i < bucketCount; i++)
            {
                DictionaryEntry<TKey, TValue> entry = buckets[i];

                while (entry != null)
                {
                    if (entry.value.Equals(value))
                        return true;

                    entry = entry.next;
                }
            }

            return false;
        }
        
        public void Clear()
        {
            Array.Clear(_buckets, 0, _buckets.Length);
            // _freeList.Clear();
            _freeCount = 0;
            Array.Clear(_freeList, 0, _freeList.Length);

            _size = 0;
            _serializedData = null;
        }
        
        private void Resize()
        {
            DictionaryEntry<TKey, TValue>[] oldBuckets = _buckets;
            int bucketCount = oldBuckets.Length;
            
            int newSize = bucketCount * 2 + 1;
            DictionaryEntry<TKey, TValue>[] newBuckets = new DictionaryEntry<TKey, TValue>[newSize];

            for (int i = 0; i < bucketCount; i++)
            {
                DictionaryEntry<TKey, TValue> entry = oldBuckets[i];

                while (entry != null)
                {
                    int newBucketIndex = Mathf.Abs(entry.key.GetHashCode() % newSize);

                    DictionaryEntry<TKey, TValue> nextEntry = entry.next;
                    entry.next = newBuckets[newBucketIndex];
                    newBuckets[newBucketIndex] = entry;

                    entry = nextEntry;
                }
            }

            _buckets = newBuckets;
            _threshold = (int)(newSize * LoadFactor);
        }
        
        private DictionaryEntry<TKey, TValue> GetFreeEntry(TKey key, TValue value)
        {
            if (_freeCount > 0)
            {
                DictionaryEntry<TKey, TValue> entry = _freeList[--_freeCount];
                
                entry.key = key;
                entry.value = value;
                
                return entry;
            }
            
            return new DictionaryEntry<TKey, TValue>(key, value);
        }
        
        private void ReturnFreeEntry(DictionaryEntry<TKey, TValue> entry)
        {
            entry.next = null;
            entry.key = default(TKey);
            entry.value = default(TValue);
            DictionaryEntry<TKey, TValue>[] freeList = _freeList;
            int freeCount = _freeCount;
            
            if (freeCount >= freeList.Length)
            {
                DictionaryEntry<TKey, TValue>[] newFreeList = new DictionaryEntry<TKey, TValue>[freeList.Length * 2];
                Array.Copy(freeList, newFreeList, freeList.Length);
                freeList = newFreeList;
                _freeList = freeList;
            }
            
            freeList[freeCount++] = entry;
            _freeCount = freeCount;
        }
        
        // Called by the serializer
        public void StoreSerializedData()
        {
            if (_size == 0)
                return;
            
            TKey[] keys = new TKey[_size];
            TValue[] values = new TValue[_size];
            
            int index = 0;
            DictionaryEntry<TKey, TValue>[] buckets = _buckets;
            int bucketCount = buckets.Length;
            
            for (int i = 0; i < bucketCount; i++)
            {
                DictionaryEntry<TKey, TValue> entry = buckets[i];

                while (entry != null)
                {
                    keys[index] = entry.key;
                    values[index] = entry.value;
                    
                    index++;
                    entry = entry.next;
                }
            }
            
            Clear();
            
            _serializedData = new SerializedDictionaryData<TKey, TValue>(keys, values);
        }

        /// <summary>
        /// Used to avoid issues where hash codes are different between editor and runtime. When the dictionary is serialized we make it store a list of keys and values instead of the internal data and then rebuild the dictionary when it is deserialized.
        /// </summary>
        private void LoadSerializedData()
        {
            if (_serializedData == null)
                return;
            
            TKey[] keys = _serializedData.keys;
            TValue[] values = _serializedData.values;
            
            Clear();
            
            int count = keys.Length;
            for (int i = 0; i < count; i++)
            {
                Add(keys[i], values[i]);
            }
        }
        
        public DictionaryIterator<TKey, TValue> GetEnumerator()
        {
            if (_serializedData != null)
                LoadSerializedData();
            
            return new DictionaryIterator<TKey, TValue>(_buckets);
        }
    }
}