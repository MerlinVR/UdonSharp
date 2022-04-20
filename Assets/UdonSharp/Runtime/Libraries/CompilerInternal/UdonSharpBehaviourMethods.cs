
using JetBrains.Annotations;
using VRC.Udon;

namespace UdonSharp.Lib.Internal
{
    public static class UdonSharpBehaviourMethods
    {
        [UsedImplicitly]
        internal static long GetUdonTypeID(UdonBehaviour behaviour)
        {
            object id = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
            if (id == null)
                return 0;

            return (long)id;
        }
        
        [UsedImplicitly]
        internal static string GetUdonTypeName(UdonBehaviour behaviour)
        {
            return (string)behaviour.GetProgramVariable(CompilerConstants.UsbTypeNameHeapKey);
        }
    }
}
