using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.Udon.Editor;

namespace UdonSharp.Editors
{
    public class UdonTypeExposureTreeView : TreeView
    {
        public bool showBaseTypeMembers = true;

        private Dictionary<string, TreeViewItem> hiearchyItems = new Dictionary<string, TreeViewItem>();

        private class TypeItemMetadata
        {
            public bool exposed = false;
            public float childExposure = 0f;
            public MemberInfo member = null;
            public bool isNamespace = false;
            public bool isType = false;
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

        private HashSet<string> exposedUdonExterns = new HashSet<string>();

        ResolverContext resolver;
        GUIStyle rowLabelStyle;

        private List<System.Type> exposedTypes;

        public UdonTypeExposureTreeView(TreeViewState state)
            :base(state)
        {
            resolver = new ResolverContext();
            rowLabelStyle = new GUIStyle(EditorStyles.label);
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
                rowLabelStyle.normal.textColor = itemMetadata.rowColor;

                if (itemMetadata.isType)
                {
                    EditorGUI.LabelField(labelRect, itemMetadata.rowName, rowLabelStyle);
                }
                else
                    EditorGUI.LabelField(labelRect, (searchString != null && searchString.Length > 0) ? itemMetadata.qualifiedRowName : itemMetadata.rowName, rowLabelStyle);
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
                if (lookupType == ExposureLookupType.Exposed && !itemData.exposed)
                    return "";
                else if (lookupType == ExposureLookupType.Unexposed && itemData.exposed)
                    return "";

                if (member.MemberType == MemberTypes.Constructor || member.MemberType == MemberTypes.Method)
                {
                    return resolver.GetUdonMethodName((MethodBase)member, false);
                }
                else if (member.MemberType == MemberTypes.Property)
                {
                    string udonNames = "";

                    if (((PropertyInfo)member).GetGetMethod() != null)
                        udonNames = resolver.GetUdonMethodName(((PropertyInfo)member).GetGetMethod(), false);
                    if (((PropertyInfo)member).GetSetMethod() != null)
                        udonNames += "\n" + resolver.GetUdonMethodName(((PropertyInfo)member).GetSetMethod(), false);

                    return udonNames;
                }
                else if (member.MemberType == MemberTypes.Field)
                {
                    return resolver.GetUdonFieldAccessorName((FieldInfo)member, FieldAccessorType.Get, false) + "\n" + resolver.GetUdonFieldAccessorName((FieldInfo)member, FieldAccessorType.Set, false);
                }
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

            return "";
        }

        private TreeViewItem GetNamespaceParent(string path, TreeViewItem root, ref int currentID)
        {
            string[] splitNamespace;

            if (path == null || path.Length == 0)
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

            if (memberInfo.MemberType == MemberTypes.Property && !((PropertyInfo)memberInfo).GetGetMethod().IsPublic)
                return;

            if (memberInfo.DeclaringType.IsEnum)
                return;

            TreeViewItem memberItem = new TreeViewItem(currentID++, parentItem.depth + 1, $"<{memberInfo.MemberType}> {memberInfo.ToString()}");
            parentItem.AddChild(memberItem);

            TypeItemMetadata itemMetadata = new TypeItemMetadata();
            itemMetadata.member = memberInfo;
            
            switch (memberInfo.MemberType)
            {
                case MemberTypes.Constructor:
                case MemberTypes.Method:
                    itemMetadata.exposed = resolver.IsValidUdonMethod(resolver.GetUdonMethodName((MethodBase)memberInfo, false));
                    break;
                case MemberTypes.Field:
                    string getAccessor = resolver.GetUdonFieldAccessorName((FieldInfo)memberInfo, FieldAccessorType.Get, false);
                    string setAccessor = resolver.GetUdonFieldAccessorName((FieldInfo)memberInfo, FieldAccessorType.Set, false);
                    exposedUdonExterns.Remove(getAccessor);
                    exposedUdonExterns.Remove(setAccessor);

                    itemMetadata.exposed = resolver.IsValidUdonMethod(getAccessor);
                    break;
                case MemberTypes.Property:
                    string getProperty = resolver.GetUdonMethodName(((PropertyInfo)memberInfo).GetGetMethod(), false);
                    exposedUdonExterns.Remove(getProperty);

                    if (((PropertyInfo)memberInfo).GetSetMethod() != null)
                    {
                        string setProperty = resolver.GetUdonMethodName(((PropertyInfo)memberInfo).GetSetMethod(), false);
                        exposedUdonExterns.Remove(setProperty);
                    }

                    itemMetadata.exposed = resolver.IsValidUdonMethod(getProperty);
                    break;
            }

            itemMetadatas.Add(memberItem, itemMetadata);

            exposedUdonExterns.Remove(GetMemberUdonName(memberItem));
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
                    metadata.rowColor = Color.green;

                if (metadata.isType)
                {
                    Color labelColor = Color.Lerp(Color.red, Color.green, metadata.childExposure);

                    float h, s, v;
                    Color.RGBToHSV(labelColor, out h, out s, out v);
                    s = 0.9f;
                    v = 0.95f;

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

        private void BuildExposedTypeList()
        {
            if (exposedTypes != null)
                return;

            try
            {
                ResolverContext resolver = new ResolverContext();

                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

                HashSet<System.Type> exposedTypeSet = new HashSet<System.Type>();

                for (int i = 0; i < assemblies.Length; ++i)
                {
                    EditorUtility.DisplayProgressBar("Processing methods and types...", $"Assembly {i + 1}/{assemblies.Length} {assemblies[i].GetName().Name}", i / (float)assemblies.Length);

                    Assembly assembly = assemblies[i];

                    if (assembly.FullName.Contains("UdonSharp") ||
                        assembly.FullName.Contains("CodeAnalysis"))
                        continue;

                    System.Type[] types = assembly.GetTypes();

                    foreach (System.Type type in types)
                    {
                        if (type.IsByRef)
                            continue;

                        string typeName = resolver.GetUdonTypeName(type);
                        if (resolver.ValidateUdonTypeName(typeName, UdonReferenceType.Type) ||
                            resolver.ValidateUdonTypeName(typeName, UdonReferenceType.Variable) ||
                            UdonEditorManager.Instance.GetTypeFromTypeString(typeName) != null)
                        {
                            exposedTypeSet.Add(type);

                            if (!type.IsGenericType && !type.IsGenericTypeDefinition)
                                exposedTypeSet.Add(type.MakeArrayType());
                        }

                        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                        bool addedType = false;

                        foreach (MethodInfo method in methods)
                        {
                            if (resolver.IsValidUdonMethod(resolver.GetUdonMethodName(method, false)))
                            {
                                if (!addedType)
                                {
                                    exposedTypeSet.Add(method.DeclaringType);
                                    addedType = true;
                                }

                                // We also want to highlight types that can be returned or taken as parameters
                                if (method.ReturnType != null &&
                                    method.ReturnType != typeof(void) &&
                                    method.ReturnType.Name != "T" &&
                                    method.ReturnType.Name != "T[]")
                                    exposedTypeSet.Add(method.ReturnType);

                                foreach (ParameterInfo parameterInfo in method.GetParameters())
                                {
                                    if (!parameterInfo.ParameterType.IsByRef)
                                        exposedTypeSet.Add(parameterInfo.ParameterType);
                                }
                            }
                        }
                    }
                }

                exposedTypes = exposedTypeSet.ToList();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.ClearProgressBar();
        }

        protected override TreeViewItem BuildRoot()
        {
            BuildExposedTypeList();

            itemMetadatas.Clear();
            hiearchyItems.Clear();

            TreeViewItem root = new TreeViewItem(0, -1);
            itemMetadatas.Add(root, new TypeItemMetadata());
            int currentID = 1;

            exposedUdonExterns.UnionWith(UdonEditorManager.Instance.GetNodeDefinitions().Select(e => e.fullName));
            exposedUdonExterns.RemoveWhere(e => e.StartsWith("Event_") || e.Contains(".__op_") || e.Contains("__SystemFunc") || e.Contains("__SystemAction"));

            // Build the namespace sections first
            foreach (System.Type type in exposedTypes)
            {
                string typeNamespace = type.Namespace;
                if (typeNamespace == null || typeNamespace == "")
                {
                    if (type.GetElementType() != null && type.GetElementType().Namespace != null)
                        typeNamespace = type.GetElementType().Namespace;
                }
                TreeViewItem namespaceItem = GetNamespaceParent(typeNamespace, root, ref currentID);
            }

            int currentTypeCount = 0;

            foreach (System.Type type in exposedTypes.OrderBy(e => e.Name))
            {
                EditorUtility.DisplayProgressBar("Adding types...", $"Adding type {type}", currentTypeCount++ / (float)exposedTypes.Count);

                string typeNamespace = type.Namespace;
                if (typeNamespace == null || typeNamespace == "")
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

                exposedUdonExterns.Remove("Variable_" + resolver.GetUdonTypeName(type));
                exposedUdonExterns.Remove("Const_" + resolver.GetUdonTypeName(type));
                exposedUdonExterns.Remove("Type_" + resolver.GetUdonTypeName(type));
                exposedUdonExterns.Remove("Type_" + resolver.GetUdonTypeName(type.MakeByRefType()));

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
                TreeViewItem internalTypeDef = new TreeViewItem(currentID++, typeParent.depth + 1, "<type> " + type.Name);
                typeParent.AddChild(internalTypeDef);
                itemMetadatas.Add(internalTypeDef, new TypeItemMetadata() { exposed = UdonEditorManager.Instance.GetTypeFromTypeString(resolver.GetUdonTypeName(type)) != null });

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
                    foreach (MethodInfo method in type.GetMethods(bindingFlags).Where(e => !e.IsSpecialName && (!type.IsArray || e.Name != "Address")))
                    {
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
        TreeViewState treeViewState;

        UdonTypeExposureTreeView treeView;

        Vector2 currentScrollPos = Vector2.zero;

        [MenuItem("Window/Udon Sharp/Class Exposure Tree")]
        static void Init()
        {
            UdonTypeExposureTree window = GetWindow<UdonTypeExposureTree>(false, "Udon Type Exposure Tree");
        }

        private void OnEnable()
        {
            if (treeViewState == null)
                treeViewState = new TreeViewState();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Class Exposure Tree", EditorStyles.boldLabel);

            if (treeView == null)
            {
                treeView = new UdonTypeExposureTreeView(treeViewState);
            }

            EditorGUI.BeginChangeCheck();
            treeView.showBaseTypeMembers = EditorGUILayout.Toggle("Show base members", treeView.showBaseTypeMembers);
            if (EditorGUI.EndChangeCheck())
                treeView.Reload();

            treeView.searchString = EditorGUILayout.TextField("Search: ", treeView.searchString);

            currentScrollPos = EditorGUILayout.BeginScrollView(currentScrollPos);

            if (treeView != null)
            {
                treeView.OnGUI(new Rect(0, 0, position.width, position.height - 60));
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
