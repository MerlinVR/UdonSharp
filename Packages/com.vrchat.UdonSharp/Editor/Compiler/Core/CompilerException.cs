
using Microsoft.CodeAnalysis;

namespace UdonSharp.Core
{
    /// <summary>
    /// Exception that describes an issue with user code
    /// </summary>
    internal class CompilerException : System.Exception
    {
        public Location Location { get; private set; }

        private CompilerException()
        {
        }

        public CompilerException(string message, Location sourceLocation = null)
            : base(message)
        {
            Location = sourceLocation;
        }

        public CompilerException(Localization.LocStr stringIdentifier, Location sourceLocation = null)
            : base(Localization.Loc.Get(stringIdentifier))
        {
            Location = sourceLocation;
        }

        public CompilerException(Localization.LocStr stringIdentifier, params object[] stringArgs)
            : base(Localization.Loc.Format(stringIdentifier, stringArgs))
        {
        }

        public CompilerException(Localization.LocStr stringIdentifier, Location sourceLocation, params object[] stringArgs)
           : base(Localization.Loc.Format(stringIdentifier, stringArgs))
        {
            Location = sourceLocation;
        }
    }
}
