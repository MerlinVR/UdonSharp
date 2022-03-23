
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UdonSharp.Compiler.Udon;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.Udon.Editor;

namespace UdonSharp.Editors
{
    public class UdonTypeExposureTreeView : TreeView
    {
        public bool showBaseTypeMembers;

        private Dictionary<string, TreeViewItem> hiearchyItems = new Dictionary<string, TreeViewItem>();

        private class TypeItemMetadata
        {
            public bool exposed;
            public float childExposure;
            public MemberInfo member;
            public bool isNamespace;
            public bool isType;
            public string udonName = "";
            public string rowName = "";
            public string qualifiedRowName = "";
            public Color rowColor = Color.black;
        }

        private enum ExposureLookupType
        {
            All,
            Exposed,
            Unexposed,
        }

        private Dictionary<TreeViewItem, TypeItemMetadata> itemMetadatas = new Dictionary<TreeViewItem, TypeItemMetadata>();

        private GUIStyle _rowLabelStyle;

        private List<Type> _exposedTypes;

        public UdonTypeExposureTreeView(TreeViewState state)
            :base(state)
        {
            _rowLabelStyle = new GUIStyle(EditorStyles.label);
            Reload();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            Rect labelRect = args.rowRect;
            labelRect.x += GetContentIndent(args.item);

            TypeItemMetadata itemMetadata = itemMetadatas[args.item];

            if (itemMetadata.isNamespace)
            {
                EditorGUI.LabelField(labelRect, itemMetadata.rowName);
            }
            else
            {
                if (args.selected)
                    _rowLabelStyle.normal.textColor = Color.white;
                else
                    _rowLabelStyle.normal.textColor = itemMetadata.rowColor;

                if (itemMetadata.isType)
                {
                    EditorGUI.LabelField(labelRect, itemMetadata.rowName, _rowLabelStyle);
                }
                else
                    EditorGUI.LabelField(labelRect, (searchString != null && searchString.Length > 0) ? itemMetadata.qualifiedRowName : itemMetadata.rowName, _rowLabelStyle);
            }

            Event current = Event.current;
            
            if (current.type == EventType.ContextClick && args.rowRect.Contains(current.mousePosition))
            {
                current.Use();

                //SelectionClick(args.item, false);
                SetSelection(new List<int>() { args.item.id });
                
                GenericMenu menu = new GenericMenu();

                if (itemMetadata.member != null)
                    menu.AddItem(new GUIContent("Copy Udon name"), false, OnClickCopyUdonName, args.item);

                if (itemMetadata.isType || itemMetadata.isNamespace)
                {
                    menu.AddItem(new GUIContent("Copy Exposed members"), false, OnClickCopyTypeItems, (args.item, ExposureLookupType.Exposed));
                    menu.AddItem(new GUIContent("Copy Unexposed members"), false, OnClickCopyTypeItems, (args.item, ExposureLookupType.Unexposed));
                    menu.AddItem(new GUIContent("Copy All members"), false, OnClickCopyTypeItems, (args.item, ExposureLookupType.All));
                }

                if (menu.GetItemCount() > 0)
                    menu.ShowAsContext();

                Repaint();
            }
        }

        private void OnClickCopyUdonName(object item)
        {
            TreeViewItem viewItem = (TreeViewItem)item;

            EditorGUIUtility.systemCopyBuffer = GetMemberUdonName(viewItem);
        }

        private void OnClickCopyTypeItems(object itemAndSearchType)
        {
            (TreeViewItem item, ExposureLookupType type) = ((TreeViewItem, ExposureLookupType))itemAndSearchType;

            EditorGUIUtility.systemCopyBuffer = GetMemberUdonName(item, type);
        }

        private string GetMemberUdonName(TreeViewItem item, ExposureLookupType lookupType = ExposureLookupType.All)
        {
            TypeItemMetadata itemData = itemMetadatas[item];

            MemberInfo member = itemData.member;

            if (member != null)
            {
                throw new NotImplementedException();
                
                // if (lookupType == ExposureLookupType.Exposed && !itemData.exposed)
                //     return "";
                // else if (lookupType == ExposureLookupType.Unexposed && itemData.exposed)
                //     return "";
                //
                // if (member.MemberType == MemberTypes.Constructor || member.MemberType == MemberTypes.Method)
                // {
                //     return resolver.GetUdonMethodName((MethodBase)member, false);
                // }
                // else if (member.MemberType == MemberTypes.Property)
                // {
                //     string udonNames = "";
                //
                //     if (((PropertyInfo)member).GetGetMethod() != null)
                //         udonNames = resolver.GetUdonMethodName(((PropertyInfo)member).GetGetMethod(), false);
                //     if (((PropertyInfo)member).GetSetMethod() != null)
                //         udonNames += "\n" + resolver.GetUdonMethodName(((PropertyInfo)member).GetSetMethod(), false);
                //
                //     return udonNames;
                // }
                // else if (member.MemberType == MemberTypes.Field)
                // {
                //     return resolver.GetUdonFieldAccessorName((FieldInfo)member, FieldAccessorType.Get, false) + "\n" + resolver.GetUdonFieldAccessorName((FieldInfo)member, FieldAccessorType.Set, false);
                // }
            }
            else
            {
                string childStringData = "";

                if (item.children != null)
                {
                    foreach (TreeViewItem childItem in item.children)
                    {
                        string childString = GetMemberUdonName(childItem, lookupType);

                        if (childString.Length > 0)
                            childStringData += childString + '\n';
                    }
                }

                return childStringData;
            }

        #pragma warning disable 162
            return "";
        #pragma warning restore 162
        }

        private TreeViewItem GetNamespaceParent(string path, TreeViewItem root, ref int currentID)
        {
            string[] splitNamespace;

            if (string.IsNullOrEmpty(path))
                splitNamespace = new string[] { "" };
            else
                splitNamespace = path.Split('.', '+');

            string currentPath = "";

            TreeViewItem parentItem = root;

            for (int i = 0; i < splitNamespace.Length; ++i)
            {
                if (i != 0)
                    currentPath += '.';

                currentPath += splitNamespace[i];

                TreeViewItem newParent;
                if (!hiearchyItems.TryGetValue(currentPath, out newParent))
                {
                    newParent = new TreeViewItem(currentID++, i, splitNamespace[i] + " <namespace>");
                    hiearchyItems.Add(currentPath, newParent);

                    parentItem.AddChild(newParent);

                    TypeItemMetadata namespaceMetadata = new TypeItemMetadata();
                    namespaceMetadata.isNamespace = true;

                    itemMetadatas.Add(newParent, namespaceMetadata);
                }

                parentItem = newParent;
            }

            return parentItem;
        }

        private void AddChildNode(TreeViewItem parentItem, MemberInfo memberInfo, ref int currentID)
        {
            var obsoleteAttribute = memberInfo.GetCustomAttribute<System.ObsoleteAttribute>();
            if (obsoleteAttribute != null)
                return;

            if (memberInfo.MemberType == MemberTypes.Property && (!((PropertyInfo)memberInfo).GetGetMethod()?.IsPublic ?? false))
                return;

            if (memberInfo.DeclaringType.IsEnum)
                return;

            string staticStr = "";
            {
                if ((memberInfo is FieldInfo fieldInfo && fieldInfo.IsStatic) ||
                    (memberInfo is PropertyInfo propertyInfo && (propertyInfo.GetGetMethod()?.IsStatic ?? false)) ||
                    (memberInfo is MethodInfo methodInfo && methodInfo.IsStatic))
                {
                    staticStr = "<Static>";
                }
            }

            TreeViewItem memberItem = new TreeViewItem(currentID++, parentItem.depth + 1, $"<{memberInfo.MemberType}>{staticStr} {memberInfo}");

            TypeItemMetadata itemMetadata = new TypeItemMetadata();
            itemMetadata.member = memberInfo;
            
            switch (memberInfo.MemberType)
            {
                case MemberTypes.Constructor:
                case MemberTypes.Method:
                    itemMetadata.exposed = CompilerUdonInterface.IsExposedToUdon(CompilerUdonInterface.GetUdonMethodName((MethodBase)memberInfo));
                    break;
                case MemberTypes.Field:
                    string getAccessor = CompilerUdonInterface.GetUdonAccessorName((FieldInfo)memberInfo, CompilerUdonInterface.FieldAccessorType.Get);
                    // string setAccessor = resolver.GetUdonFieldAccessorName((FieldInfo)memberInfo, CompilerUdonInterface.FieldAccessorType.Set, false);

                    itemMetadata.exposed = CompilerUdonInterface.IsExposedToUdon(getAccessor);
                    break;
                case MemberTypes.Property:
                    MethodInfo getMethod = ((PropertyInfo) memberInfo).GetGetMethod();

                    if (getMethod == null)
                        return;
                    
                    string getProperty = CompilerUdonInterface.GetUdonMethodName(getMethod);

                    // if (((PropertyInfo)memberInfo).GetSetMethod() != null)
                    // {
                    //     string setProperty = resolver.GetUdonMethodName(((PropertyInfo)memberInfo).GetSetMethod(), false);
                    // }

                    itemMetadata.exposed = CompilerUdonInterface.IsExposedToUdon(getProperty);
                    break;
            }
            
            parentItem.AddChild(memberItem);

            itemMetadatas.Add(memberItem, itemMetadata);
        }

        private (int, int) BuildDrawInfo(TreeViewItem item)
        {
            (int, int) countTotal = (0, 0);
            TypeItemMetadata metadata = itemMetadatas[item];

            if (!metadata.isNamespace && !metadata.isType && item.depth >= 0)
            {
                countTotal = (metadata.exposed ? 1 : 0, 1);
            }
            else
            {
                if (item.children != null)
                {
                    foreach (TreeViewItem child in item.children)
                    {
                        (int, int) childCounts = BuildDrawInfo(child);
                        countTotal.Item1 += childCounts.Item1;
                        countTotal.Item2 += childCounts.Item2;
                    }
                }
            }

            metadata.childExposure = countTotal.Item1 / (float)countTotal.Item2;

            if (metadata.isNamespace)
            {
                metadata.rowName = item.displayName;
                metadata.qualifiedRowName = item.displayName;
            }
            else
            {
                metadata.rowColor = Color.red;
                if (metadata.exposed)
                {
                    metadata.rowColor = Color.green;

                    if (!EditorGUIUtility.isProSkin)
                    {
                        metadata.rowColor = new Color(0.2f, 0.6f, 0.2f);
                    }
                }

                if (metadata.isType)
                {
                    Color labelColor = Color.Lerp(Color.red, Color.green, metadata.childExposure);

                    float h, s, v;
                    Color.RGBToHSV(labelColor, out h, out s, out v);
                    s = 0.9f;
                    v = 0.95f;

                    if (!EditorGUIUtility.isProSkin)
                    {
                        v = Mathf.Lerp(0.62f, 0.55f, metadata.childExposure);
                    }

                    metadata.rowColor = Color.HSVToRGB(h, s, v);

                    metadata.rowName = metadata.qualifiedRowName = $"({metadata.childExposure * 100f:0.##}%) {item.displayName}";
                }
                else
                {
                    metadata.rowName = metadata.qualifiedRowName = item.displayName;
                    if (metadata.member != null && metadata.member.DeclaringType != null)
                        metadata.qualifiedRowName = metadata.member.DeclaringType.Name + "." + metadata.rowName;
                }
            }

            return countTotal;
        }

        // Mostly because assembly.GetTypes doesn't return types that are nested under other nested types, which people really shouldn't do, but this is here for completeness
        private static List<System.Type> GetNestedTypes(System.Type type)
        {
            List<System.Type> nestedTypes = new List<System.Type>();

            foreach (System.Type nestedType in type.GetNestedTypes())
            {
                nestedTypes.Add(nestedType);

                nestedTypes.AddRange(GetNestedTypes(nestedType));
            }

            return nestedTypes;
        }

        private static int _assemblyCounter = 0;
        
        private void BuildExposedTypeList()
        {
            if (_exposedTypes != null)
                return;
            
            // Stopwatch timer = Stopwatch.StartNew();
            
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                object typeSetLock = new object();
                HashSet<Type> exposedTypeSet = new HashSet<Type>();
                
                int mainThreadID = Thread.CurrentThread.ManagedThreadId;
                _assemblyCounter = 0;
                int totalAssemblies = assemblies.Length;
                
                Parallel.ForEach(assemblies, new ParallelOptions { MaxDegreeOfParallelism = 3 }, assembly =>
                {
                    if (assembly.FullName.Contains("UdonSharp") ||
                        assembly.FullName.Contains("CodeAnalysis"))
                        return;

                    Interlocked.Increment(ref _assemblyCounter);

                    if (Thread.CurrentThread.ManagedThreadId == mainThreadID) // Can only be called from the main thread, since Parallel.ForEach uses the calling thread for some loops we just only run this in that thread.
                        EditorUtility.DisplayProgressBar("Processing methods and types...", $"{_assemblyCounter}/{totalAssemblies}", _assemblyCounter / (float)totalAssemblies);

                    Type[] assemblyTypes = assembly.GetTypes();

                    List<Type> types = new List<Type>();

                    foreach (Type assemblyType in assemblyTypes)
                    {
                        types.Add(assemblyType);
                        types.AddRange(GetNestedTypes(assemblyType));
                    }

                    types = types.Distinct().ToList();

                    HashSet<Type> localExposedTypeSet = new HashSet<Type>();

                    foreach (Type type in types)
                    {
                        if (type.IsByRef)
                            continue;

                        string typeName = CompilerUdonInterface.GetUdonTypeName(type);
                        if (UdonEditorManager.Instance.GetTypeFromTypeString(typeName) != null)
                        {
                            localExposedTypeSet.Add(type);

                            if (!type.IsGenericType && !type.IsGenericTypeDefinition)
                                localExposedTypeSet.Add(type.MakeArrayType());
                        }

                        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                        foreach (MethodInfo method in methods)
                        {
                            if (CompilerUdonInterface.IsExposedToUdon(CompilerUdonInterface.GetUdonMethodName(method)))
                            {
                                localExposedTypeSet.Add(method.DeclaringType);

                                // We also want to highlight types that can be returned or taken as parameters
                                if (method.ReturnType != typeof(void) &&
                                    method.ReturnType.Name != "T" &&
                                    method.ReturnType.Name != "T[]")
                                {
                                    localExposedTypeSet.Add(method.ReturnType);

                                    if (!method.ReturnType.IsArray && !method.ReturnType.IsGenericType &&
                                        !method.ReturnType.IsGenericTypeDefinition)
                                        localExposedTypeSet.Add(method.ReturnType.MakeArrayType());
                                }

                                foreach (ParameterInfo parameterInfo in method.GetParameters())
                                {
                                    if (!parameterInfo.ParameterType.IsByRef)
                                    {
                                        localExposedTypeSet.Add(parameterInfo.ParameterType);

                                        if (!parameterInfo.ParameterType.IsArray)
                                            localExposedTypeSet.Add(parameterInfo.ParameterType.MakeArrayType());
                                    }
                                }
                            }
                        }

                        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        {
                            MethodInfo propertyGetter = property.GetGetMethod();
                            if (propertyGetter == null)
                                continue;

                            if (CompilerUdonInterface.IsExposedToUdon(CompilerUdonInterface.GetUdonMethodName(propertyGetter)))
                            {
                                Type returnType = propertyGetter.ReturnType;

                                localExposedTypeSet.Add(property.DeclaringType);

                                if (returnType != typeof(void) &&
                                    returnType.Name != "T" &&
                                    returnType.Name != "T[]")
                                {
                                    localExposedTypeSet.Add(returnType);

                                    if (!returnType.IsArray && !returnType.IsGenericType &&
                                        !returnType.IsGenericTypeDefinition)
                                        localExposedTypeSet.Add(returnType.MakeArrayType());
                                }
                            }
                        }

                        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        {
                            if (field.DeclaringType?.FullName ==
                                null) // Fix some weird types in Odin that don't have a name for their declaring type
                                continue;

                            if (CompilerUdonInterface.IsExposedToUdon(CompilerUdonInterface.GetUdonAccessorName(field, CompilerUdonInterface.FieldAccessorType.Get)))
                            {
                                Type returnType = field.FieldType;

                                localExposedTypeSet.Add(field.DeclaringType);

                                if (returnType != typeof(void) &&
                                    returnType.Name != "T" &&
                                    returnType.Name != "T[]")
                                {
                                    localExposedTypeSet.Add(returnType);

                                    if (!returnType.IsArray && !returnType.IsGenericType &&
                                        !returnType.IsGenericTypeDefinition)
                                        localExposedTypeSet.Add(returnType.MakeArrayType());
                                }
                            }
                        }
                    }

                    if (localExposedTypeSet.Count == 0) 
                        return;
                    
                    lock (typeSetLock)
                    {
                        exposedTypeSet.UnionWith(localExposedTypeSet);
                    }
                });

                _exposedTypes = exposedTypeSet.ToList();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _exposedTypes.RemoveAll(e => e.Name == "T" || e.Name == "T[]");
            
            // Debug.Log($"Elapsed time {timer.Elapsed.TotalSeconds * 1000.0}ms");
        }

        protected override TreeViewItem BuildRoot()
        {
            BuildExposedTypeList();

            itemMetadatas.Clear();
            hiearchyItems.Clear();

            TreeViewItem root = new TreeViewItem(0, -1);
            itemMetadatas.Add(root, new TypeItemMetadata());
            int currentID = 1;

            // Build the namespace sections first
            foreach (Type type in _exposedTypes)
            {
                string typeNamespace = type.Namespace;
                if (string.IsNullOrEmpty(typeNamespace))
                {
                    if (type.GetElementType() != null && type.GetElementType().Namespace != null)
                        typeNamespace = type.GetElementType().Namespace;
                }
                TreeViewItem namespaceItem = GetNamespaceParent(typeNamespace, root, ref currentID);
            }

            int currentTypeCount = 0;

            foreach (Type type in _exposedTypes.OrderBy(e => e.Name))
            {
                if (currentTypeCount % 30 == 0)
                    EditorUtility.DisplayProgressBar("Adding types...", $"Adding type {type}", currentTypeCount / (float)_exposedTypes.Count);

                currentTypeCount++;
                
                // if (ShouldHideTypeTopLevel(type, true))
                //     continue;

                string typeNamespace = type.Namespace;
                if (string.IsNullOrEmpty(typeNamespace))
                {
                    if (type.GetElementType() != null && type.GetElementType().Namespace != null)
                        typeNamespace = type.GetElementType().Namespace;
                }

                TreeViewItem namespaceParent = GetNamespaceParent(typeNamespace, root, ref currentID);

                string typeTypeName = "";

                if (type.IsEnum)
                    typeTypeName = " <enum>";
                else if (type.IsValueType)
                    typeTypeName = " <struct>";
                else if (type.IsArray)
                    typeTypeName = " <array>";
                else
                    typeTypeName = " <class>";

                TreeViewItem typeParent = new TreeViewItem(currentID++, namespaceParent.depth + 1, type.Name + typeTypeName);
                namespaceParent.AddChild(typeParent);
                itemMetadatas.Add(typeParent, new TypeItemMetadata() { isType = true });

                //if (!type.IsEnum)
                //{
                //    // Variable definition
                //    TreeViewItem variableDef = new TreeViewItem(currentID++, typeParent.depth + 1, "<variable> " + type.Name);
                //    typeParent.AddChild(variableDef);
                //    itemMetadatas.Add(variableDef, new TypeItemMetadata() { exposed = resolver.ValidateUdonTypeName(resolver.GetUdonTypeName(type), UdonReferenceType.Variable) });
                //}

                // Type definition
                //TreeViewItem typeDef = new TreeViewItem(currentID++, typeParent.depth + 1, "<type> " + type.Name);
                //typeParent.AddChild(typeDef);
                //itemMetadatas.Add(typeDef, new TypeItemMetadata() { exposed = resolver.ValidateUdonTypeName(resolver.GetUdonTypeName(type), UdonReferenceType.Type) });

                // Internal type
                TreeViewItem internalTypeDef = new TreeViewItem(currentID++, typeParent.depth + 1, "<Type> " + type.Name);
                typeParent.AddChild(internalTypeDef);
                itemMetadatas.Add(internalTypeDef, new TypeItemMetadata() { exposed = UdonEditorManager.Instance.GetTypeFromTypeString(CompilerUdonInterface.GetUdonTypeName(type)) != null });

                // Const definition
                //if (!type.IsArray && !type.IsEnum)
                //{
                //    TreeViewItem constDef = new TreeViewItem(currentID++, typeParent.depth + 1, "<const> " + type.Name);
                //    typeParent.AddChild(constDef);
                //    itemMetadatas.Add(constDef, new TypeItemMetadata() { exposed = resolver.ValidateUdonTypeName(resolver.GetUdonTypeName(type), UdonReferenceType.Const) });
                //}

                BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                if (!showBaseTypeMembers)
                    bindingFlags |= BindingFlags.DeclaredOnly;

                foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    AddChildNode(typeParent, constructor, ref currentID);
                }

                foreach (FieldInfo field in type.GetFields(bindingFlags))
                {
                    AddChildNode(typeParent, field, ref currentID);
                }

                foreach (PropertyInfo property in type.GetProperties(bindingFlags))
                {
                    AddChildNode(typeParent, property, ref currentID);
                }

                if (!type.IsEnum)
                {
                    foreach (MethodInfo method in type.GetMethods(bindingFlags).Where(e => (!type.IsArray || e.Name != "Address")))
                    {
                        if (method.IsSpecialName && !method.Name.StartsWith("op_"))
                            continue;

                        AddChildNode(typeParent, method, ref currentID);
                    }
                }
            }

            EditorUtility.ClearProgressBar();

            BuildDrawInfo(root);

            //foreach (string exposedExtern in exposedUdonExterns)
            //{
            //    Debug.Log(exposedExtern);
            //}

            return root;
        }
    }

    public class UdonTypeExposureTree : EditorWindow
    {
        [SerializeField] 
        private TreeViewState treeViewState;

        private UdonTypeExposureTreeView _treeView;

        private Vector2 _currentScrollPos = Vector2.zero;

        [MenuItem("VRChat SDK/Udon Sharp/Class Exposure Tree")]
        private static void Init()
        {
            GetWindow<UdonTypeExposureTree>(false, "Udon Type Exposure Tree");
        }

        private void OnEnable()
        {
            if (treeViewState == null)
                treeViewState = new TreeViewState();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Class Exposure Tree", EditorStyles.boldLabel);

            if (_treeView == null)
            {
                _treeView = new UdonTypeExposureTreeView(treeViewState);
            }

            EditorGUI.BeginChangeCheck();
            _treeView.showBaseTypeMembers = EditorGUILayout.Toggle("Show base members", _treeView.showBaseTypeMembers);
            
            if (EditorGUI.EndChangeCheck())
                _treeView.Reload();

            _treeView.searchString = EditorGUILayout.TextField("Search: ", _treeView.searchString);

            _currentScrollPos = EditorGUILayout.BeginScrollView(_currentScrollPos);

            _treeView?.OnGUI(new Rect(0, 0, position.width, position.height - 80));

            EditorGUILayout.EndScrollView();
        }
    }
}
