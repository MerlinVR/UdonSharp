
using Microsoft.CodeAnalysis;

namespace UdonSharp.Core
{
    /// <summary>
    /// Exception for when an extern or type is not currently exposed by Udon
    /// </summary>
    internal class NotExposedException : CompilerException
    {
        public NotExposedException(string message, Location sourceLocation = null)
            : base(message, sourceLocation)
        {
        }

        public NotExposedException(Localization.LocStr stringIdentifier, Location sourceLocation = null)
            : base(Localization.Loc.Get(stringIdentifier), sourceLocation)
        {
        }

        public NotExposedException(Localization.LocStr stringIdentifier, params object[] stringArgs)
            : base(Localization.Loc.Format(stringIdentifier, stringArgs))
        {
        }

        public NotExposedException(Localization.LocStr stringIdentifier, Location sourceLocation, params object[] stringArgs)
           : base(Localization.Loc.Format(stringIdentifier, stringArgs), sourceLocation)
        {
        }
    }
}
