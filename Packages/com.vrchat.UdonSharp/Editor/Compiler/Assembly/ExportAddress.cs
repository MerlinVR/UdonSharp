
using JetBrains.Annotations;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Assembly
{
    internal class ExportAddress
    {
        public enum AddressKind
        {
            String,
            Uint,
        }
        
        public AddressKind Kind { get; }
        
        /// <summary>
        /// The symbol this export belongs to
        /// </summary>
        public Symbol AddressSymbol { get; }
        
        public string AddressString { get; private set; }
        
        public uint AddressUint { get; private set; }

        public bool IsResolved { get; private set; }

        // public ExportAddress(string address, [NotNull] Symbol symbol)
        // {
        //     Kind = AddressKind.String;
        //     AddressString = address;
        //     AddressSymbol = symbol;
        //     IsResolved = true;
        // }
        //
        // public ExportAddress(uint address, [NotNull] Symbol symbol)
        // {
        //     Kind = AddressKind.Uint;
        //     AddressUint = address;
        //     AddressSymbol = symbol;
        //     IsResolved = true;
        // }

        public ExportAddress(AddressKind kind, [NotNull] Symbol symbol)
        {
            Kind = kind;
            AddressSymbol = symbol;
        }

        public void ResolveAddress(uint address)
        {
            IsResolved = true;
            AddressUint = address;
        }

        public void ResolveAddress(string address)
        {
            IsResolved = true;
            AddressString = address;
        }

        protected bool Equals(ExportAddress other)
        {
            return Equals(AddressSymbol, other.AddressSymbol) && AddressString == other.AddressString && AddressUint == other.AddressUint;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ExportAddress) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (AddressSymbol != null ? AddressSymbol.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (AddressString != null ? AddressString.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int) AddressUint;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"address: {(Kind == AddressKind.String ? AddressString : AddressUint.ToString())}, symbol: {AddressSymbol}";
        }
    }
}
