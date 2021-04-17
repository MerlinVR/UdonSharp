
namespace UdonSharp.Core
{
    /// <summary>
    /// Exception that describes an issue with user code
    /// </summary>
    internal class CompilerException : System.Exception
    {
        public CompilerException()
        {
        }

        public CompilerException(string message)
            : base(message)
        {
        }

        public CompilerException(Localization.LocStr stringIdentifier)
            : base(Localization.Loc.Get(stringIdentifier))
        {
        }

        public CompilerException(Localization.LocStr stringIdentifier, params object[] stringArgs)
            : base(Localization.Loc.Format(stringIdentifier, stringArgs))
        {
        }
    }
}
