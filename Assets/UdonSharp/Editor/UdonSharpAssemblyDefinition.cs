
using UnityEditorInternal;
using UnityEngine;

namespace UdonSharpEditor
{
    [CreateAssetMenu(menuName = "U# Assembly Definition", fileName = "New Assembly Descriptor", order = 95)]
    public class UdonSharpAssemblyDefinition : ScriptableObject
    {
        public AssemblyDefinitionAsset sourceAssembly;
    }
}
