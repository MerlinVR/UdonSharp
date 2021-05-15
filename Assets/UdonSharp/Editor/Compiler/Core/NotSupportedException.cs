
using Microsoft.CodeAnalysis;

namespace UdonSharp.Core
{
    /// <summary>
    /// Exception for when something is not supported by U#
    /// </summary>
    internal class NotSupportedException : CompilerException
    {
        public NotSupportedException(string message, Location sourceLocation = null)
            : base(message, sourceLocation)
        {
        }

        public NotSupportedException(Localization.LocStr stringIdentifier, Location sourceLocation = null)
            : base(Localization.Loc.Get(stringIdentifier), sourceLocation)
        {
        }

        public NotSupportedException(Localization.LocStr stringIdentifier, params object[] stringArgs)
            : base(Localization.Loc.Format(stringIdentifier, stringArgs))
        {
        }

        public NotSupportedException(Localization.LocStr stringIdentifier, Location sourceLocation, params object[] stringArgs)
           : base(Localization.Loc.Format(stringIdentifier, stringArgs), sourceLocation)
        {
        }
    }
}
