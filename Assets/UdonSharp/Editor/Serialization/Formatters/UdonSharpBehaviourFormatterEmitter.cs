

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace UdonSharp.Serialization
{
    internal class UdonSharpBehaviourFormatterEmitter
    {
        const string RUNTIME_ASSEMBLY_NAME = "UdonSharp.Serialization.RuntimeEmittedFormatters";

        public delegate void ReadDataMethodDelegate<T>(IValueStorage[] sourceObject, ref T targetObject, bool includeNonSerialized);

        public delegate void WriteDataMethodDelegate<T>(IValueStorage[] targetObject, ref T sourceObject, bool includeNonSerialized);

        // Force a ref equality lookup in case VRC implements an Equals or GetHashCode overrides in the future that don't act like we need them to
        class RefEqualityComparer<RefT> : EqualityComparer<RefT> where RefT : class
        {
            public override bool Equals(RefT x, RefT y) { return ReferenceEquals(x, y); }
            public override int GetHashCode(RefT obj) { return RuntimeHelpers.GetHashCode(obj); }
        }
        
        static IHeapStorage CreateHeapStorage(UdonBehaviour behaviour)
        {
            if (EditorApplication.isPlaying)
            {
                UdonHeapStorageInterface heapStorageInterface = new UdonHeapStorageInterface(behaviour);

                if (heapStorageInterface.IsValid)
                    return heapStorageInterface;
                else
                    return null;
            }
            else
            {
                return new UdonVariableStorageInterface(behaviour);
            }
        }

        class UdonBehaviourHeapData
        {
            public IHeapStorage heapStorage;
            public IValueStorage[] heapFieldValues; // Direct references to each field in order on the heap storage.
        }

        class EmittedFormatter<T> : Formatter<T> where T : UdonSharpBehaviour 
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

            class UdonSharpBehaviourFormatterManager
            {
                static Dictionary<UdonBehaviour, UdonBehaviourHeapData> heapDataLookup = new Dictionary<UdonBehaviour, UdonBehaviourHeapData>(new RefEqualityComparer<UdonBehaviour>());
                public static string[] fieldLayout;

                public static UdonBehaviourHeapData GetHeapData(UdonBehaviour udonBehaviour)
                {
                    UdonBehaviourHeapData heapData;
                    if (heapDataLookup.TryGetValue(udonBehaviour, out heapData))
                    {
                        return heapData;
                    }

                    if (fieldLayout == null)
                        throw new System.NullReferenceException($"Formatter manager {typeof(UdonSharpBehaviourFormatterManager).FullName} has not been initialized.");

                    IHeapStorage heapStorage = CreateHeapStorage(udonBehaviour);
                    if (heapStorage == null)
                        return null;

                    IValueStorage[] heapFieldValues = new IValueStorage[fieldLayout.Length];

                    for (int i = 0; i < heapFieldValues.Length; ++i)
                    {
                        heapFieldValues[i] = heapStorage.GetElementStorage(fieldLayout[i]);
                    }

                    heapData = new UdonBehaviourHeapData() { heapStorage = heapStorage, heapFieldValues = heapFieldValues };

                    heapDataLookup.Add(udonBehaviour, heapData);

                    return heapData;
                }
            }

            ReadDataMethodDelegate<T> readDelegate;
            WriteDataMethodDelegate<T> writeDelegate;

            public EmittedFormatter(ReadDataMethodDelegate<T> readDelegate, WriteDataMethodDelegate<T> writeDelegate)
            {
                this.readDelegate = readDelegate;
                this.writeDelegate = writeDelegate;
            }

            public override void Read(ref T targetObject, IValueStorage sourceObject)
            {
                UdonBehaviourHeapData heapStorage = UdonSharpBehaviourFormatterManager.GetHeapData((UdonBehaviour)sourceObject.Value);

                if (heapStorage != null)
                    readDelegate(heapStorage.heapFieldValues, ref targetObject, EditorApplication.isPlaying);
            }

            public override void Write(IValueStorage targetObject, T sourceObject)
            {
                UdonBehaviourHeapData heapStorage = UdonSharpBehaviourFormatterManager.GetHeapData(UdonSharpEditorUtility.GetBackingUdonBehaviour(sourceObject));

                if (heapStorage != null)
                    writeDelegate(heapStorage.heapFieldValues, ref sourceObject, EditorApplication.isPlaying);
            }
        }

        static Dictionary<System.Type, IFormatter> formatters = new Dictionary<System.Type, IFormatter>();

        static readonly object emitLock = new object();
        static System.Reflection.Emit.AssemblyBuilder runtimeEmittedAssembly;
        static ModuleBuilder runtimeEmittedModule;

        public static Formatter<T> GetFormatter<T>() where T : UdonSharpBehaviour
        {
            lock (emitLock)
            {
                IFormatter formatter;
                if (formatters.TryGetValue(typeof(T), out formatter))
                {
                    return (Formatter<T>)formatter;
                }

                List<FieldInfo> serializedFieldList = new List<FieldInfo>();
                List<FieldInfo> nonSerializedFieldList = new List<FieldInfo>();

                FieldInfo[] allFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (FieldInfo field in allFields)
                {
                    if (field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                        continue;

                    if ((field.IsPublic && field.GetAttribute<System.NonSerializedAttribute>() == null) ||
                        (!field.IsPublic && field.GetAttribute<SerializeField>() != null))
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

                Dictionary<System.Type, FieldBuilder> serializerFields;

                BuildHelperType(typeof(T), publicFields, privateFields, out serializerFields);

                System.Type formatterType = typeof(EmittedFormatter<>).MakeGenericType(typeof(T));
                System.Delegate readDel, writeDel;

                // Read
                {
                    System.Type readDelegateType = typeof(ReadDataMethodDelegate<>).MakeGenericType(typeof(T));
                    MethodInfo readDataMethod = formatterType.GetMethods(Flags.InstancePublic).Where(e => e.Name == "Read" && e.GetParameters().Length == 2).First();
                    DynamicMethod readMethod = new DynamicMethod($"Dynamic_{typeof(T).GetCompilableNiceFullName()}_Read", null, new[] { typeof(IValueStorage[]), typeof(T).MakeByRefType(), typeof(bool) }, true);

                    foreach (ParameterInfo param in readDataMethod.GetParameters())
                        readMethod.DefineParameter(param.Position, param.Attributes, param.Name);

                    EmitReadMethod(readMethod.GetILGenerator(), typeof(T), publicFields, privateFields, serializerFields);

                    readDel = readMethod.CreateDelegate(readDelegateType);
                }

                // Write
                {
                    System.Type writeDelegateType = typeof(WriteDataMethodDelegate<>).MakeGenericType(typeof(T));
                    MethodInfo writeDataMethod = formatterType.GetMethods(Flags.InstancePublic).Where(e => e.Name == "Write" && e.GetParameters().Length == 2).First();
                    DynamicMethod writeMethod = new DynamicMethod($"Dynamic_{typeof(T).GetCompilableNiceFullName()}_Write", null, new[] { typeof(IValueStorage[]), typeof(T).MakeByRefType(), typeof(bool) }, true);

                    foreach (ParameterInfo param in writeDataMethod.GetParameters())
                        writeMethod.DefineParameter(param.Position, param.Attributes, param.Name);

                    EmitWriteMethod(writeMethod.GetILGenerator(), typeof(T), publicFields, privateFields, serializerFields);

                    writeDel = writeMethod.CreateDelegate(writeDelegateType);
                }

                formatter = (Formatter<T>)System.Activator.CreateInstance(typeof(EmittedFormatter<T>), readDel, writeDel);

                formatters.Add(typeof(T), formatter);
                return (Formatter<T>)formatter;
            }
        }

        static void InitializeRuntimeAssemblyBuilder()
        {
            if (runtimeEmittedAssembly == null)
            {
                AssemblyName assemblyName = new AssemblyName(RUNTIME_ASSEMBLY_NAME);

                assemblyName.CultureInfo = System.Globalization.CultureInfo.InvariantCulture;
                assemblyName.Flags = AssemblyNameFlags.None;
                assemblyName.ProcessorArchitecture = ProcessorArchitecture.MSIL;
                assemblyName.VersionCompatibility = System.Configuration.Assemblies.AssemblyVersionCompatibility.SameDomain;

                runtimeEmittedAssembly = System.AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            }

            if (runtimeEmittedModule == null)
            {
                runtimeEmittedModule = runtimeEmittedAssembly.DefineDynamicModule(RUNTIME_ASSEMBLY_NAME, true);
            }
        }

        static System.Type BuildHelperType(System.Type formattedType,
                                           FieldInfo[] publicFields,
                                           FieldInfo[] privateFields,
                                           out Dictionary<System.Type, FieldBuilder> serializerFields)
        {
            string generatedTypeName = $"{runtimeEmittedModule.Name}.{formattedType.GetCompilableNiceFullName()}___{formattedType.Assembly.GetName()}___FormatterHelper___{System.Guid.NewGuid().ToString()}";
            TypeBuilder typeBuilder = runtimeEmittedModule.DefineType(generatedTypeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);

            serializerFields = new Dictionary<System.Type, FieldBuilder>();

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

            {
                MethodInfo serializerCreateMethod = typeof(Serializer).GetMethod("CreatePooled", Flags.StaticPublic, null, new System.Type[] { }, null);
                //MethodInfo typeofMethod = typeof(System.Type).GetMethod("GetTypeFromHandle", Flags.StaticPublic, null, new[] { typeof(System.RuntimeTypeHandle) }, null);
                ConstructorBuilder staticConstructor = typeBuilder.DefineTypeInitializer();
                ILGenerator generator = staticConstructor.GetILGenerator();

                foreach (var entry in serializerFields)
                {
                    generator.Emit(OpCodes.Call, serializerCreateMethod.MakeGenericMethod(entry.Key));
                    generator.Emit(OpCodes.Stsfld, entry.Value);
                }

                generator.Emit(OpCodes.Ret);
            }

            return typeBuilder.CreateType();
        }

        // Is this even needed or will the generator do this anyways?
        static void EmitConstInt(ILGenerator gen, int value)
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
                    if (value < 128)
                        gen.Emit(OpCodes.Ldc_I4_S, value);
                    else
                        gen.Emit(OpCodes.Ldc_I4, value);

                    break;
            }
        }

        static void EmitReadMethod(ILGenerator generator, 
                                   System.Type formattedType, 
                                   FieldInfo[] publicFields, 
                                   FieldInfo[] privateFields, 
                                   Dictionary<System.Type, FieldBuilder> serializerFields)
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

        static void EmitWriteMethod(ILGenerator generator,
                                    System.Type formattedType,
                                    FieldInfo[] publicFields,
                                    FieldInfo[] privateFields,
                                    Dictionary<System.Type, FieldBuilder> serializerFields)
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
