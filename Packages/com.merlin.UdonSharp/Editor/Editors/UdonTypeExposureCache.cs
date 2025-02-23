using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UdonSharp.Editors
{
    public class UdonTypeExposureCache : ScriptableObject
    {
        private const string CacheAssetPath = "Library/UdonSharpCache/UdonTypeExposureCache.asset";

        private static UdonTypeExposureCache _Instance;

        [SerializeField] private List<string> _ExposedTypeNames;
        private List<Type> _ExposedTypes;
        public List<Type> ExposedTypes
        {
            get => _ExposedTypes;
            set
            {
                if(value != null)
                {
                    _ExposedTypes = value;
                    _ExposedTypeNames = value.Select(t => t.AssemblyQualifiedName).ToList();
                }
                else
                {
                    _ExposedTypes = null;
                    _ExposedTypeNames = null;
                }

                Save(false);
            }
        }

        public static UdonTypeExposureCache Instance
        {
            get
            {
                if(_Instance == null)
                {
                    var instance = InternalEditorUtility.LoadSerializedFileAndForget(CacheAssetPath);
                    _Instance = (instance != null && instance.Length > 0 && instance[0] != null) ? (UdonTypeExposureCache)instance[0] : CreateInstance<UdonTypeExposureCache>();
                    _Instance.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

                    if(_Instance._ExposedTypeNames != null && _Instance._ExposedTypeNames.Count > 0)
                    {
                        _Instance._ExposedTypes = new();

                        foreach(var typeName in _Instance._ExposedTypeNames)
                        {
                            if(typeName == null) continue;

                            var type = Type.GetType(typeName, false);
                            if(type == null) continue;

                            _Instance._ExposedTypes.Add(type);
                        }
                    }
                    else
                    {
                        _Instance._ExposedTypes = null;
                        _Instance._ExposedTypeNames = null;
                    }
                }

                return _Instance;
            }
        }

        private void Save(bool saveAsText)
        {
            if(_Instance == null) return;

            var directoryName = Path.GetDirectoryName(CacheAssetPath);
            if(!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);
            InternalEditorUtility.SaveToSerializedFileAndForget(new UdonTypeExposureCache[] { _Instance }, CacheAssetPath, saveAsText);
        }
    }
}
