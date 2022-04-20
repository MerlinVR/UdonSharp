
namespace UdonSharp.Compiler.Symbols
{
    internal interface IExternSymbol
    {
        /// <summary>
        /// The signature for the given extern that Udon uses to identify it
        /// </summary>
        string ExternSignature { get; }
    }

    internal interface IExternAccessor
    {
        /// <summary>
        /// Implemented by fields and properties, for setting a value on an extern
        /// </summary>
        string ExternSetSignature { get; }
        
        /// <summary>
        /// Implemented by fields and properties, for getting a value on an extern
        /// </summary>
        string ExternGetSignature { get; }
    }
}
