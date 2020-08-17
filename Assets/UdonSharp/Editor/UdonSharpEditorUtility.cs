
using JetBrains.Annotations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;

namespace UdonSharpEditor
{
    public static class UdonSharpEditorUtility
    {
        /// <summary>
        /// Creates a new UdonAssemblyProgramAsset from an UdonSharpProgramAsset for the sake of portability. Most info used for the inspector gets stripped so this isn't a great solution for remotely complex assets.
        /// </summary>
        /// <param name="udonSharpProgramAsset">The source program asset</param>
        /// <param name="savePath">The save path for the asset file. Save path is only needed here because Udon needs a GUID for saving the serialized program asset and it'd be a pain to break that requirement at the moment</param>
        /// <returns>The exported UdonAssemblyProgramAsset</returns>
        [PublicAPI]
        public static UdonAssemblyProgramAsset UdonSharpProgramToAssemblyProgram(UdonSharpProgramAsset udonSharpProgramAsset, string savePath)
        {
            if (EditorApplication.isPlaying)
                throw new System.NotSupportedException("USharpEditorUtility.UdonSharpProgramToAssemblyProgram() cannot be called in play mode");

            UdonAssemblyProgramAsset newProgramAsset = ScriptableObject.CreateInstance<UdonAssemblyProgramAsset>();
            AssetDatabase.CreateAsset(newProgramAsset, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            newProgramAsset = AssetDatabase.LoadAssetAtPath<UdonAssemblyProgramAsset>(savePath);

            FieldInfo assemblyField = typeof(UdonAssemblyProgramAsset).GetField("udonAssembly", BindingFlags.NonPublic | BindingFlags.Instance);
            udonSharpProgramAsset.CompileCsProgram();

            assemblyField.SetValue(newProgramAsset, assemblyField.GetValue(udonSharpProgramAsset));

            MethodInfo assembleMethod = typeof(UdonAssemblyProgramAsset).GetMethod("AssembleProgram", BindingFlags.NonPublic | BindingFlags.Instance);
            assembleMethod.Invoke(newProgramAsset, new object[] { });

            IUdonProgram uSharpProgram = udonSharpProgramAsset.GetRealProgram();
            FieldInfo assemblyProgramGetter = typeof(UdonProgramAsset).GetField("program", BindingFlags.NonPublic | BindingFlags.Instance);
            IUdonProgram assemblyProgram = (IUdonProgram)assemblyProgramGetter.GetValue(newProgramAsset);

            if (uSharpProgram == null || assemblyProgram == null)
                return null;

            string[] symbols = uSharpProgram.SymbolTable.GetSymbols();

            foreach (string symbol in symbols)
            {
                uint symbolAddress = uSharpProgram.SymbolTable.GetAddressFromSymbol(symbol);
                System.Type symbolType = uSharpProgram.Heap.GetHeapVariableType(symbolAddress);
                object symbolValue = uSharpProgram.Heap.GetHeapVariable(symbolAddress);
                
                assemblyProgram.Heap.SetHeapVariable(assemblyProgram.SymbolTable.GetAddressFromSymbol(symbol), symbolValue, symbolType);
            }

            EditorUtility.SetDirty(newProgramAsset);

            newProgramAsset.SerializedProgramAsset.StoreProgram(assemblyProgram);
            EditorUtility.SetDirty(newProgramAsset.SerializedProgramAsset);

            AssetDatabase.SaveAssets();

            // This doesn't work unfortunately due to how Udon tries to locate the serialized asset when importing an assembly
            //string serializedAssetPath = $"{Path.GetDirectoryName(savePath)}/{Path.GetFileNameWithoutExtension(savePath)}_serialized.asset";
            
            //AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(newProgramAsset.SerializedProgramAsset), serializedAssetPath);
            //AssetDatabase.SaveAssets();

            return newProgramAsset;
        }

        /// <summary>
        /// Deletes an UdonSharp program asset and the serialized program asset associated with it
        /// </summary>
        /// <param name="programAsset"></param>
        [PublicAPI]
        public static void DeleteProgramAsset(UdonSharpProgramAsset programAsset)
        {
            if (programAsset == null)
                return;

            AbstractSerializedUdonProgramAsset serializedAsset = programAsset.GetSerializedUdonProgramAsset();

            if (serializedAsset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(serializedAsset);
                serializedAsset = AssetDatabase.LoadAssetAtPath<AbstractSerializedUdonProgramAsset>(assetPath);

                if (serializedAsset != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            string programAssetPath = AssetDatabase.GetAssetPath(programAsset);

            programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programAssetPath);

            if (programAsset != null)
                AssetDatabase.DeleteAsset(programAssetPath);
        }

        /// <summary>
        /// Converts a set of UdonSharpBehaviour components to their equivalent UdonBehaviour components
        /// </summary>
        /// <param name="components"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonBehaviour[] ConvertToUdonBehaviours(UdonSharpBehaviour[] components)
        {
            return ConvertToUdonBehavioursInternal(components, false, false);
        }

        /// <summary>
        /// Converts a set of UdonSharpBehaviour components to their equivalent UdonBehaviour components
        /// Registers an Undo operation for the conversion
        /// </summary>
        /// <param name="components"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonBehaviour[] ConvertToUdonBehavioursWithUndo(UdonSharpBehaviour[] components)
        {
            return ConvertToUdonBehavioursInternal(components, true, false);
        }

        private static UdonSharpProgramAsset GetUdonSharpProgramAsset(MonoScript programScript)
        {
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            foreach (string dataGuid in udonSharpDataAssets)
            {
                UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid));

                if (programAsset && programAsset.sourceCsScript == programScript)
                {
                    return programAsset;
                }
            }

            return null;
        }

        [PublicAPI]
        public static UdonSharpProgramAsset GetUdonSharpProgramAsset(UdonSharpBehaviour udonSharpBehaviour)
        {
            return GetUdonSharpProgramAsset(MonoScript.FromMonoBehaviour(udonSharpBehaviour));
        }

        internal static UdonBehaviour[] ConvertToUdonBehavioursInternal(UdonSharpBehaviour[] components, bool shouldUndo, bool createScriptPrompt)
        {
            components = components.Distinct().ToArray();

            if (shouldUndo)
                Undo.RegisterCompleteObjectUndo(components, "Convert to UdonBehaviour");

            List<UdonBehaviour> createdComponents = new List<UdonBehaviour>();
            foreach (UdonSharpBehaviour targetObject in components)
            {
                MonoScript behaviourScript = MonoScript.FromMonoBehaviour(targetObject);
                UdonSharpProgramAsset programAsset = GetUdonSharpProgramAsset(behaviourScript);

                if (programAsset == null)
                {
                    if (createScriptPrompt)
                    {
                        string scriptPath = AssetDatabase.GetAssetPath(behaviourScript);
                        string scriptDirectory = Path.GetDirectoryName(scriptPath);
                        string scriptFileName = Path.GetFileNameWithoutExtension(scriptPath);

                        string assetPath = Path.Combine(scriptDirectory, $"{scriptFileName}.asset").Replace('\\', '/');

                        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null)
                        {
                            if (!EditorUtility.DisplayDialog("Existing file found", $"Asset file {assetPath} already exists, do you want to overwrite it?", "Ok", "Cancel"))
                                continue;
                        }

                        programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                        programAsset.sourceCsScript = behaviourScript;
                        programAsset.CompileCsProgram();

                        AssetDatabase.CreateAsset(programAsset, assetPath);
                        AssetDatabase.SaveAssets();

                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                    else
                        continue;
                }

                GameObject targetGameObject = targetObject.gameObject;

                UdonBehaviour udonBehaviour = null;

                if (shouldUndo)
                    udonBehaviour = Undo.AddComponent<UdonBehaviour>(targetGameObject);
                else
                    udonBehaviour = targetGameObject.AddComponent<UdonBehaviour>();

                udonBehaviour.programSource = programAsset;

                //if (shouldUndo)
                //    Undo.DestroyObjectImmediate(targetObject);
                //else
                //    Object.DestroyImmediate(targetObject);

                if (shouldUndo)
                    Undo.RegisterCompleteObjectUndo(targetObject, "Convert C# to U# behaviour");
                
                UdonSharpEditorUtility.SetBackingUdonBehaviour(targetObject, udonBehaviour);
                UdonSharpEditorUtility.CopyProxyToBacker(targetObject);

                targetObject.hideFlags = HideFlags.DontSaveInBuild |
#if !UDONSHARP_DEBUG
                                         HideFlags.HideInInspector |
#endif
                                         HideFlags.DontSaveInEditor;

                targetObject.enabled = false;

                createdComponents.Add(udonBehaviour);
            }

            return createdComponents.ToArray();
        }

        private static readonly FieldInfo _backingBehaviourField = typeof(UdonSharpBehaviour).GetField("_backingUdonBehaviour", BindingFlags.NonPublic | BindingFlags.Instance);
        [PublicAPI]
        public static UdonBehaviour GetBackingUdonBehaviour(UdonSharpBehaviour behaviour)
        {
            return (UdonBehaviour)_backingBehaviourField.GetValue(behaviour);
        }

        internal static void SetBackingUdonBehaviour(UdonSharpBehaviour behaviour, UdonBehaviour backingBehaviour)
        {
            _backingBehaviourField.SetValue(behaviour, backingBehaviour);
        }

        /// <summary>
        /// Returns true if the given behaviour is a proxy behaviour that's linked to an UdonBehaviour.
        /// Proxy behaviours are used to interact with Udon heap data using a C# proxy of the UdonBehaviour's UdonSharpProgramAsset program, 
        ///   and should not be used to do anything other than interact with this data
        /// </summary>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool IsProxyBehaviour(UdonSharpBehaviour behaviour)
        {
            return GetBackingUdonBehaviour(behaviour) != null;
        }

        static Dictionary<UdonBehaviour, UdonSharpBehaviour> _proxyBehaviourLookup = new Dictionary<UdonBehaviour, UdonSharpBehaviour>();

        /// <summary>
        /// Finds an existing proxy behaviour, if none exists returns null
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <param name="copyBackerToProxy"></param>
        /// <returns></returns>
        public static UdonSharpBehaviour FindProxyBehaviour(UdonBehaviour udonBehaviour, bool copyBackerToProxy = true)
        {
            if (_proxyBehaviourLookup.TryGetValue(udonBehaviour, out UdonSharpBehaviour proxyBehaviour))
            {
                if (proxyBehaviour != null)
                {
                    if (copyBackerToProxy)
                        CopyBackerToProxy(proxyBehaviour);

                    return proxyBehaviour;
                }
                else
                {
                    _proxyBehaviourLookup.Remove(udonBehaviour);
                }
            }

            UdonSharpBehaviour[] behaviours = udonBehaviour.GetComponents<UdonSharpBehaviour>();

            foreach (UdonSharpBehaviour udonSharpBehaviour in behaviours)
            {
                IUdonBehaviour backingBehaviour = GetBackingUdonBehaviour(udonSharpBehaviour);
                if (backingBehaviour != null && ReferenceEquals(backingBehaviour, udonBehaviour))
                {
                    _proxyBehaviourLookup.Add(udonBehaviour, udonSharpBehaviour);

                    if (copyBackerToProxy)
                        CopyBackerToProxy(udonSharpBehaviour);

                    return udonSharpBehaviour;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the C# version of an UdonSharpBehaviour that proxies an UdonBehaviour with the program asset for the matching UdonSharpBehaviour type
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        /// 
        [PublicAPI]
        public static UdonSharpBehaviour GetProxyBehaviour(UdonBehaviour udonBehaviour, bool copyBackerToProxy = true)
        {
            if (udonBehaviour == null)
                throw new System.ArgumentNullException("Source Udon Behaviour cannot be null");

            if (udonBehaviour.programSource == null)
                throw new System.ArgumentNullException("Program source on UdonBehaviour cannot be null");

            UdonSharpProgramAsset udonSharpProgram = udonBehaviour.programSource as UdonSharpProgramAsset;

            if (udonSharpProgram == null)
                throw new System.ArgumentException("UdonBehaviour must be using an UdonSharp program");

            UdonSharpBehaviour proxyBehaviour = FindProxyBehaviour(udonBehaviour, copyBackerToProxy);

            if (proxyBehaviour)
                return proxyBehaviour;

            // We've failed to find an existing proxy behaviour so we need to create one
            System.Type scriptType = udonSharpProgram.sourceCsScript.GetClass();

            proxyBehaviour = (UdonSharpBehaviour)udonBehaviour.gameObject.AddComponent(scriptType);
            proxyBehaviour.hideFlags = HideFlags.DontSaveInBuild |
#if !UDONSHARP_DEBUG
                                       HideFlags.HideInInspector |
#endif
                                       HideFlags.DontSaveInEditor;
            proxyBehaviour.enabled = false;

            SetBackingUdonBehaviour(proxyBehaviour, udonBehaviour);

            _proxyBehaviourLookup.Add(udonBehaviour, proxyBehaviour);

            if (copyBackerToProxy)
                CopyBackerToProxy(proxyBehaviour);

            return proxyBehaviour;
        }

        [PublicAPI]
        public static void CopyProxyToBacker(UdonSharpBehaviour proxy)
        {
            Profiler.BeginSample("CopyProxyToBacker");
            SimpleValueStorage<UdonBehaviour> udonBehaviourStorage = new SimpleValueStorage<UdonBehaviour>(GetBackingUdonBehaviour(proxy));
            Serializer.CreatePooled(proxy.GetType()).WriteWeak(udonBehaviourStorage, proxy);
            Profiler.EndSample();
        }

        [PublicAPI]
        public static void CopyBackerToProxy(UdonSharpBehaviour proxy)
        {
            Profiler.BeginSample("CopyBackerToProxy");
            SimpleValueStorage<UdonBehaviour> udonBehaviourStorage = new SimpleValueStorage<UdonBehaviour>(GetBackingUdonBehaviour(proxy));

            object proxyObj = proxy;
            Serializer.CreatePooled(proxy.GetType()).ReadWeak(ref proxyObj, udonBehaviourStorage);
            Profiler.EndSample();
        }

        [PublicAPI]
        public static UdonBehaviour CreateBehavourForProxy(UdonSharpBehaviour udonSharpBehaviour)
        {
            UdonBehaviour backingBehaviour = GetBackingUdonBehaviour(udonSharpBehaviour);

            if (backingBehaviour == null)
            {
                backingBehaviour = udonSharpBehaviour.gameObject.AddComponent<UdonBehaviour>();
                backingBehaviour.programSource = GetUdonSharpProgramAsset(udonSharpBehaviour);
            }

            CopyProxyToBacker(udonSharpBehaviour);

            return backingBehaviour;
        }
    }
}
