
namespace UdonSharp.Compiler
{
    [System.Flags]
    public enum MethodDeclFlags
    {
        None = 0,
        Public = 1,
        Private = 2,
        RecursiveMethod = 4,
    }

    public class ParameterDefinition
    {
        public System.Type type;
        public string symbolName;
        public object defaultValue; // Not supported yet, this may be changed to contain an expression node
        public SymbolDefinition paramSymbol;
    }

    public class MethodDefinition
    {
        public MethodDeclFlags declarationFlags;
        public string originalMethodName;
        public string uniqueMethodName;
        public SymbolDefinition returnSymbol;
        public ParameterDefinition[] parameters;
        public JumpLabel methodUdonEntryPoint;
        public JumpLabel methodUserCallStart; // This differs from the Udon entry point, it is advanced past the instructions that reset the return point to 0xFFFFFFFF to allow returns
        public JumpLabel methodReturnPoint;

        public bool IsUserFunction { get { return originalMethodName == uniqueMethodName; } } // This will be changed if we add user overloads
    }
}
