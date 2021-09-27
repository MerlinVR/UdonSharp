
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal class ParameterSymbol : Symbol
    {
        public TypeSymbol Type { get; private set; }
        public IConstantValue DefaultValue { get; }
        public bool IsParams => RoslynSymbol.IsParams;
        public RefKind RefKind => RoslynSymbol.RefKind;

        public bool IsByRef => RoslynSymbol.RefKind != RefKind.None;
        public bool IsOut => RoslynSymbol.RefKind == RefKind.Out || RoslynSymbol.RefKind == RefKind.Ref;

        public new IParameterSymbol RoslynSymbol => (IParameterSymbol) base.RoslynSymbol;

        public override bool IsBound => true;
        
        public MethodSymbol ContainingSymbol { get; private set; }

        public ParameterSymbol(IParameterSymbol sourceSymbol, AbstractPhaseContext context)
            :base(sourceSymbol, context)
        {
            Type = context.GetTypeSymbol(RoslynSymbol.Type);
            
            if (RoslynSymbol.OriginalDefinition != RoslynSymbol)
                OriginalSymbol = context.GetSymbol(RoslynSymbol.OriginalDefinition);

            if (RoslynSymbol.HasExplicitDefaultValue)
            {
                if (Type.IsEnum)
                {
                    DefaultValue = (IConstantValue) Activator.CreateInstance(
                        typeof(ConstantValue<>).MakeGenericType(Type.UdonType.SystemType),
                        Enum.ToObject(Type.UdonType.SystemType, RoslynSymbol.ExplicitDefaultValue));
                }
                else
                {
                    DefaultValue = (IConstantValue) Activator.CreateInstance(
                        typeof(ConstantValue<>).MakeGenericType(Type.UdonType.SystemType),
                        RoslynSymbol.ExplicitDefaultValue);
                }
            }
        }

        public ParameterSymbol(TypeSymbol type, AbstractPhaseContext context)
            :base(null, context)
        {
            Type = type;
        }

        public override void Bind(BindContext context)
        {
            Type = context.GetTypeSymbol(RoslynSymbol.Type);
            ContainingSymbol = (MethodSymbol)context.GetSymbol(RoslynSymbol.ContainingSymbol);
        }
    }
}
