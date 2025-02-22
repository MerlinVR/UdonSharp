
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;
using UdonSharp.Core;

namespace UdonSharp.Compiler.Symbols
{
    internal class UdonSharpBehaviourFieldSymbol : FieldSymbol
    {
        public UdonSharpBehaviourFieldSymbol(IFieldSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
            if (IsBound)
                return;
            
            base.Bind(context);

            var fieldChangeAttribute = GetAttribute<FieldChangeCallbackAttribute>();
            if (fieldChangeAttribute != null)
            {
                string targetPropertyName = fieldChangeAttribute.CallbackPropertyName;

                PropertySymbol targetProperty = ContainingType.GetMember<PropertySymbol>(targetPropertyName, context);

                if (targetProperty == null)
                    throw new CompilerException($"Target property '{targetPropertyName}' for FieldChangeCallback was not found");

                if (targetProperty.Type != Type)
                    throw new CompilerException($"Target property type '{targetProperty.Type}' did not match field type");
                
                targetProperty.MarkFieldCallback(this);
            }
        }
    }
}
