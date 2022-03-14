
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Symbols
{
    internal class ExternTypeSymbol : TypeSymbol, IExternSymbol
    {
        public ExternTypeSymbol(INamedTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            TryGetSystemType(sourceSymbol, out Type systemType);
            SystemType = systemType;
            
            Type udonType = UdonSharpUtils.UserTypeToUdonType(SystemType);

            UdonType = (ExternTypeSymbol)(udonType == SystemType ? this : context.GetUdonTypeSymbol(sourceSymbol));

            ExternSignature = CompilerUdonInterface.GetUdonTypeName(this);
        }

        public ExternTypeSymbol(IArrayTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            TryGetSystemType(sourceSymbol, out Type systemType);
            SystemType = systemType;
            
            Type udonType = UdonSharpUtils.UserTypeToUdonType(SystemType);

            UdonType = (ExternTypeSymbol)(udonType == SystemType ? this : context.GetUdonTypeSymbol(sourceSymbol));

            ExternSignature = CompilerUdonInterface.GetUdonTypeName(UdonType);
        }

        public override bool IsExtern => true;

        public override bool IsBound => true;
        
        /// <summary>
        /// The associated System.Type type for a given metadata symbol.
        /// </summary>
        public Type SystemType { get; }

        public override void Bind(BindContext context)
        {
            // Extern types do not need to be bound as they will always have their members bound lazily only when referenced and fields just exist already.
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, AbstractPhaseContext context)
        {
            switch (roslynSymbol.Kind)
            {
                case SymbolKind.Method:
                    IMethodSymbol methodSymbol = (IMethodSymbol) roslynSymbol;
                    return methodSymbol.MethodKind == MethodKind.BuiltinOperator ? new ExternBuiltinOperatorSymbol(methodSymbol, context) : new ExternMethodSymbol(methodSymbol, context);
                case SymbolKind.Field:
                    return new ExternFieldSymbol((IFieldSymbol)roslynSymbol, context);
                case SymbolKind.Property:
                    return new ExternPropertySymbol((IPropertySymbol) roslynSymbol, context);
                case SymbolKind.Parameter:
                    return new ParameterSymbol((IParameterSymbol)roslynSymbol, context);
            }

            throw new NotSupportedException("Cannot create symbol for Roslyn symbol of kind " + roslynSymbol.Kind);
        }

        public string ExternSignature { get; }
    }
}
