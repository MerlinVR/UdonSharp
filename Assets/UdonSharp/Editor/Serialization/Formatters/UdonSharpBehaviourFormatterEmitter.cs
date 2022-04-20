
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UdonSharp.Compiler.Symbols;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace UdonSharp.Serialization
{
    internal static class UdonSharpBehaviourFormatterEmitter
    {
        private const string RUNTIME_ASSEMBLY_NAME = "UdonSharp.Serialization.RuntimeEmittedFormatters";

        private delegate void ReadDataMethodDelegate<T>(IValueStorage[] sourceObject, ref T targetObject, bool includeNonSerialized);

        private delegate void WriteDataMethodDelegate<T>(IValueStorage[] targetObject, ref T sourceObject, bool includeNonSerialized);

        // Force a ref equality lookup in case VRC implements an Equals or GetHashCode overrides in the future that don't act like we need them to
        private class RefEqualityComparer<TRef> : EqualityComparer<TRef> where TRef : class
        {
            public override bool Equals(TRef x, TRef y) { return ReferenceEquals(x, y); }
            public override int GetHashCode(TRef obj) { return RuntimeHelpers.GetHashCode(obj); }
        }

        private static IHeapStorage CreateHeapStorage(UdonBehaviour behaviour)
        {
            if (EditorApplication.isPlaying && !UsbSerializationContext.UseHeapSerialization)
            {
                UdonHeapStorageInterface heapStorageInterface = new UdonHeapStorageInterface(behaviour);

                return heapStorageInterface.IsValid ? heapStorageInterface : null;
            }

            return new UdonVariableStorageInterface(behaviour);
        }

        private class UdonBehaviourHeapData
        {
            public IHeapStorage heapStorage;
            public IValueStorage[] heapFieldValues; // Direct references to each field in order on the heap storage.
        }

        private class EmittedFormatter<T> : Formatter<T> where T : UdonSharpBehaviour 
        {
            // Initialize field layout for T
            public static void Init(FieldInfo[] publicFields, FieldInfo[] privateFields)
            {
                if (typeof(T) == typeof(UdonSharpBehaviour))
                {
                    Debug.LogError("Attempted to initialize UdonSharpBehaviour emitted formatter for UdonSharpBehaviour exact type. This is not allowed.");
                    return;
                }

                string[] fieldLayout = new string[publicFields.Length + privateFields.Length];

                for (int i = 0; i < publicFields.Length; ++i) fieldLayout[i] = publicFields[i].Name;
                for (int i = publicFields.Length; i < publicFields.Length + privateFields.Length; ++i) fieldLayout[i] = privateFields[i - publicFields.Length].Name;

                UdonSharpBehaviourFormatterManager.fieldLayout = fieldLayout;
            }

            private class UdonSharpBehaviourFormatterManager
            {
                // ReSharper disable once StaticMemberInGenericType
                private static Dictionary<UdonBehaviour, UdonBehaviourHeapData> _heapDataLookup = new Dictionary<UdonBehaviour, UdonBehaviourHeapData>(new RefEqualityComparer<UdonBehaviour>());
                // ReSharper disable once StaticMemberInGenericType
                public static string[] fieldLayout;

                public static UdonBehaviourHeapData GetHeapData(UdonBehaviour udonBehaviour)
                {
                    if (!UsbSerializationContext.UseHeapSerialization && _heapDataLookup.TryGetValue(udonBehaviour, out var heapData))
                        return heapData;

                    if (fieldLayout == null)
                        throw new NullReferenceException($"Formatter manager {typeof(UdonSharpBehaviourFormatterManager).FullName} has not been initialized.");

                    IHeapStorage heapStorage = CreateHeapStorage(udonBehaviour);
                    if (heapStorage == null)
                        return null;

                    IValueStorage[] heapFieldValues = new IValueStorage[fieldLayout.Length];

                    for (int i = 0; i < heapFieldValues.Length; ++i)
                        heapFieldValues[i] = heapStorage.GetElementStorage(fieldLayout[i]);

                    heapData = new UdonBehaviourHeapData() { heapStorage = heapStorage, heapFieldValues = heapFieldValues };

                    if (!UsbSerializationContext.UseHeapSerialization)
                        _heapDataLookup.Add(udonBehaviour, heapData);

                    return heapData;
                }
            }

            private ReadDataMethodDelegate<T> readDelegate;
            private WriteDataMethodDelegate<T> writeDelegate;

            public EmittedFormatter(ReadDataMethodDelegate<T> readDelegate, WriteDataMethodDelegate<T> writeDelegate)
            {
                this.readDelegate = readDelegate;
                this.writeDelegate = writeDelegate;
            }

            public override void Read(ref T targetObject, IValueStorage sourceObject)
            {
                UdonBehaviourHeapData heapStorage = UdonSharpBehaviourFormatterManager.GetHeapData((UdonBehaviour)sourceObject.Value);

                if (heapStorage != null)
                    readDelegate(heapStorage.heapFieldValues, ref targetObject, EditorApplication.isPlaying && !UsbSerializationContext.CurrentPolicy.IsPreBuildSerialize);
            }

            public override void Write(IValueStorage targetObject, T sourceObject)
            {
                UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(sourceObject);
                UdonBehaviourHeapData heapStorage = UdonSharpBehaviourFormatterManager.GetHeapData(backingBehaviour);

                if (heapStorage == null) 
                    return;
                
                writeDelegate(heapStorage.heapFieldValues, ref sourceObject, EditorApplication.isPlaying && !UsbSerializationContext.CurrentPolicy.IsPreBuildSerialize);
                    
                if (!UsbSerializationContext.CollectDependencies && !EditorApplication.isPlaying)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(backingBehaviour);
            }
        }

        private static Dictionary<Type, IFormatter> _formatters = new Dictionary<Type, IFormatter>();

        private static readonly object _emitLock = new object();
        private static AssemblyBuilder _runtimeEmittedAssembly;
        private static ModuleBuilder _runtimeEmittedModule;

        private static readonly MethodInfo _getMethodInfoGeneric = typeof(UdonSharpBehaviourFormatterEmitter)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(e => e.Name == nameof(GetFormatter) && e.IsGenericMethod);

        private static Dictionary<Type, MethodInfo> _generatedMethods = new Dictionary<Type, MethodInfo>();

        public static IFormatter GetFormatter(Type type)
        {
            lock (_emitLock)
            {
                if (!_generatedMethods.TryGetValue(type, out var formatterGet))
                {
                    formatterGet = _getMethodInfoGeneric.MakeGenericMethod(type);
                    _generatedMethods.Add(type, formatterGet);
                }

                return (IFormatter)formatterGet.Invoke(null, Array.Empty<object>());
            }
        }

        public static Formatter<T> GetFormatter<T>() where T : UdonSharpBehaviour
        {
            lock (_emitLock)
            {
                if (_formatters.TryGetValue(typeof(T), out IFormatter formatter))
                {
                    return (Formatter<T>)formatter;
                }

                List<FieldInfo> serializedFieldList = new List<FieldInfo>();
                List<FieldInfo> nonSerializedFieldList = new List<FieldInfo>();

                Stack<Type> baseTypes = new Stack<Type>();

                Type currentType = typeof(T);

                while (currentType != null && 
                       currentType != typeof(UdonSharpBehaviour))
                {
                    baseTypes.Push(currentType);
                    currentType = currentType.BaseType;
                }

                List<FieldInfo> allFields = new List<FieldInfo>();

                while (baseTypes.Count > 0)
                {
                    currentType = baseTypes.Pop();
                    allFields.AddRange(currentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                }

                foreach (FieldInfo field in allFields)
                {
                    // if (field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    //     continue;

                    if (FieldSymbol.IsFieldSerialized(field))
                    {
                        serializedFieldList.Add(field);
                    }
                    else
                    {
                        nonSerializedFieldList.Add(field);
                    }
                }

                FieldInfo[] publicFields = serializedFieldList.ToArray();
                FieldInfo[] privateFields = nonSerializedFieldList.ToArray();
                
                EmittedFormatter<T>.Init(publicFields, privateFields);

                InitializeRuntimeAssemblyBuilder();

                BuildHelperType(typeof(T), publicFields, privateFields, out var serializerFields);

                Type formatterType = typeof(EmittedFormatter<>).MakeGenericType(typeof(T));
                Delegate readDel, writeDel;

                // Read
                {
                    Type readDelegateType = typeof(ReadDataMethodDelegate<>).MakeGenericType(typeof(T));
                    MethodInfo readDataMethod = formatterType.GetMethods(Flags.InstancePublic).First(e => e.Name == "Read" && e.GetParameters().Length == 2);
                    DynamicMethod readMethod = new DynamicMethod($"Dynamic_{typeof(T).GetCompilableNiceFullName()}_Read", null, new[] { typeof(IValueStorage[]), typeof(T).MakeByRefType(), typeof(bool) }, true);

                    foreach (ParameterInfo param in readDataMethod.GetParameters())
                        readMethod.DefineParameter(param.Position, param.Attributes, param.Name);

                    EmitReadMethod(readMethod.GetILGenerator(), publicFields, privateFields, serializerFields);

                    readDel = readMethod.CreateDelegate(readDelegateType);
                }

                // Write
                {
                    Type writeDelegateType = typeof(WriteDataMethodDelegate<>).MakeGenericType(typeof(T));
                    MethodInfo writeDataMethod = formatterType.GetMethods(Flags.InstancePublic).First(e => e.Name == "Write" && e.GetParameters().Length == 2);
                    DynamicMethod writeMethod = new DynamicMethod($"Dynamic_{typeof(T).GetCompilableNiceFullName()}_Write", null, new[] { typeof(IValueStorage[]), typeof(T).MakeByRefType(), typeof(bool) }, true);

                    foreach (ParameterInfo param in writeDataMethod.GetParameters())
                        writeMethod.DefineParameter(param.Position, param.Attributes, param.Name);

                    EmitWriteMethod(writeMethod.GetILGenerator(), publicFields, privateFields, serializerFields);

                    writeDel = writeMethod.CreateDelegate(writeDelegateType);
                }

                formatter = (Formatter<T>)Activator.CreateInstance(typeof(EmittedFormatter<T>), readDel, writeDel);

                _formatters.Add(typeof(T), formatter);
                return (Formatter<T>)formatter;
            }
        }

        private static void InitializeRuntimeAssemblyBuilder()
        {
            if (_runtimeEmittedAssembly == null)
            {
                AssemblyName assemblyName = new AssemblyName(RUNTIME_ASSEMBLY_NAME)
                {
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    Flags = AssemblyNameFlags.None,
                    ProcessorArchitecture = ProcessorArchitecture.MSIL,
                    VersionCompatibility = System.Configuration.Assemblies.AssemblyVersionCompatibility.SameDomain
                };

                _runtimeEmittedAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            }

            if (_runtimeEmittedModule == null)
            {
                _runtimeEmittedModule = _runtimeEmittedAssembly.DefineDynamicModule(RUNTIME_ASSEMBLY_NAME, true);
            }
        }
        
        private static readonly MethodInfo _serializerCreateMethod = typeof(Serializer).GetMethod(nameof(Serializer.CreatePooled), BindingFlags.Public | BindingFlags.Static, null, new Type[] { }, null);

        private static void BuildHelperType(Type formattedType,
                                            FieldInfo[] publicFields,
                                            FieldInfo[] privateFields,
                                            out Dictionary<Type, FieldBuilder> serializerFields)
        {
            string generatedTypeName = $"{_runtimeEmittedModule.Name}.{formattedType.GetCompilableNiceFullName()}___{formattedType.Assembly.GetName()}___FormatterHelper___{Guid.NewGuid().ToString()}";
            TypeBuilder typeBuilder = _runtimeEmittedModule.DefineType(generatedTypeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);

            serializerFields = new Dictionary<Type, FieldBuilder>();

            foreach (FieldInfo info in publicFields)
            {
                if (!serializerFields.ContainsKey(info.FieldType))
                {
                    serializerFields.Add(info.FieldType, typeBuilder.DefineField($"{info.FieldType.GetCompilableNiceFullName()}___Serializer", typeof(Serializer<>).MakeGenericType(info.FieldType), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly));
                }
            }

            foreach (FieldInfo info in privateFields)
            {
                if (!serializerFields.ContainsKey(info.FieldType))
                {
                    serializerFields.Add(info.FieldType, typeBuilder.DefineField($"{info.FieldType.GetCompilableNiceFullName()}___Serializer", typeof(Serializer<>).MakeGenericType(info.FieldType), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly));
                }
            }

            //MethodInfo typeofMethod = typeof(System.Type).GetMethod("GetTypeFromHandle", Flags.StaticPublic, null, new[] { typeof(System.RuntimeTypeHandle) }, null);
            ConstructorBuilder staticConstructor = typeBuilder.DefineTypeInitializer();
            ILGenerator generator = staticConstructor.GetILGenerator();

            foreach (var entry in serializerFields)
            {
                generator.Emit(OpCodes.Call, _serializerCreateMethod.MakeGenericMethod(entry.Key));
                generator.Emit(OpCodes.Stsfld, entry.Value);
            }

            generator.Emit(OpCodes.Ret);

            typeBuilder.CreateType();
        }

        // Is this even needed or will the generator do this anyways?
        private static void EmitConstInt(ILGenerator gen, int value)
        {
            switch (value)
            {
                case 0:
                    gen.Emit(OpCodes.Ldc_I4_0);
                    break;
                case 1:
                    gen.Emit(OpCodes.Ldc_I4_1);
                    break;
                case 2:
                    gen.Emit(OpCodes.Ldc_I4_2);
                    break;
                case 3:
                    gen.Emit(OpCodes.Ldc_I4_3);
                    break;
                case 4:
                    gen.Emit(OpCodes.Ldc_I4_4);
                    break;
                case 5:
                    gen.Emit(OpCodes.Ldc_I4_5);
                    break;
                case 6:
                    gen.Emit(OpCodes.Ldc_I4_6);
                    break;
                case 7:
                    gen.Emit(OpCodes.Ldc_I4_7);
                    break;
                case 8:
                    gen.Emit(OpCodes.Ldc_I4_8);
                    break;
                default:
                    gen.Emit(value < 128 ? OpCodes.Ldc_I4_S : OpCodes.Ldc_I4, value);

                    break;
            }
        }

        private static void EmitReadMethod(ILGenerator generator, 
                                   FieldInfo[] publicFields, 
                                   FieldInfo[] privateFields, 
                                   Dictionary<Type, FieldBuilder> serializerFields)
        {
            for (int i = 0; i < publicFields.Length; ++i)
            {
                FieldInfo currentField = publicFields[i];
                FieldBuilder serializerField = serializerFields[currentField.FieldType];
                generator.Emit(OpCodes.Ldsfld, serializerField); // Load serializer field
                
                generator.Emit(OpCodes.Ldarg_1); // Load the serialized type arg
                generator.Emit(OpCodes.Ldind_Ref); // Read by ref type
                generator.Emit(OpCodes.Ldflda, currentField); // Get field address to read to

                generator.Emit(OpCodes.Ldarg_0); // Load the IValueStorage array
                EmitConstInt(generator, i); // Emit the element index
                generator.Emit(OpCodes.Ldelem_Ref); // Read element, push to stack
                
                generator.Emit(OpCodes.Callvirt, serializerField.FieldType.GetMethod("Read")); // Call the serializer's Read method
            }

            Label skipNonSerializedLabel = generator.DefineLabel();
            generator.Emit(OpCodes.Ldarg_2); // load includeNonSerialized bool arg
            generator.Emit(OpCodes.Brfalse, skipNonSerializedLabel); // Jump to exit if includeNonSerialized is false

            for (int i = 0; i < privateFields.Length; ++i)
            {
                FieldInfo currentField = privateFields[i];
                FieldBuilder serializerField = serializerFields[currentField.FieldType];
                generator.Emit(OpCodes.Ldsfld, serializerField); // Load serializer field

                generator.Emit(OpCodes.Ldarg_1); // Load the serialized type arg
                generator.Emit(OpCodes.Ldind_Ref); // Read by ref type
                generator.Emit(OpCodes.Ldflda, currentField); // Get field address to read to

                generator.Emit(OpCodes.Ldarg_0); // Load the IValueStorage array
                EmitConstInt(generator, i + publicFields.Length); // Emit the element index
                generator.Emit(OpCodes.Ldelem_Ref); // Read element, push to stack

                generator.Emit(OpCodes.Callvirt, serializerField.FieldType.GetMethod("Read")); // Call the serializer's Read method
            }
            
            generator.MarkLabel(skipNonSerializedLabel);

            generator.Emit(OpCodes.Ret);
        }

        private static void EmitWriteMethod(ILGenerator generator,
                                    FieldInfo[] publicFields,
                                    FieldInfo[] privateFields,
                                    Dictionary<Type, FieldBuilder> serializerFields)
        {
            for (int i = 0; i < publicFields.Length; ++i)
            {
                FieldInfo currentField = publicFields[i];
                FieldBuilder serializerField = serializerFields[currentField.FieldType];
                generator.Emit(OpCodes.Ldsfld, serializerField); // Load serializer field
                
                generator.Emit(OpCodes.Ldarg_0); // Load the IValueStorage array
                EmitConstInt(generator, i); // Emit the element index
                generator.Emit(OpCodes.Ldelem_Ref); // Read element, push to stack

                generator.Emit(OpCodes.Ldarg_1); // Load the serialized type arg
                generator.Emit(OpCodes.Ldind_Ref); // Read by ref type
                generator.Emit(OpCodes.Ldflda, currentField); // Get field address to read from

                generator.Emit(OpCodes.Callvirt, serializerField.FieldType.GetMethod("Write")); // Call the serializer's Write method
            }

            Label skipNonSerializedLabel = generator.DefineLabel();
            generator.Emit(OpCodes.Ldarg_2); // load includeNonSerialized bool arg
            generator.Emit(OpCodes.Brfalse, skipNonSerializedLabel); // Jump to exit if includeNonSerialized is false

            for (int i = 0; i < privateFields.Length; ++i)
            {
                FieldInfo currentField = privateFields[i];
                FieldBuilder serializerField = serializerFields[currentField.FieldType];
                generator.Emit(OpCodes.Ldsfld, serializerField); // Load serializer field

                generator.Emit(OpCodes.Ldarg_0); // Load the IValueStorage array
                EmitConstInt(generator, i + publicFields.Length); // Emit the element index
                generator.Emit(OpCodes.Ldelem_Ref); // Read element, push to stack

                generator.Emit(OpCodes.Ldarg_1); // Load the serialized type arg
                generator.Emit(OpCodes.Ldind_Ref); // Read by ref type
                generator.Emit(OpCodes.Ldflda, currentField); // Get field address to read from

                generator.Emit(OpCodes.Callvirt, serializerField.FieldType.GetMethod("Write")); // Call the serializer's Write method
            }
            
            generator.MarkLabel(skipNonSerializedLabel);

            generator.Emit(OpCodes.Ret);
        }
    }
}
