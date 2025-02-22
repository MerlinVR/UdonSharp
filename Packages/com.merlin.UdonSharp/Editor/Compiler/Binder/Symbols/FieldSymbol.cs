
using System;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Binder;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Serialization.OdinSerializer;
using NotSupportedException = UdonSharp.Core.NotSupportedException;

namespace UdonSharp.Compiler.Symbols
{
    internal class FieldSymbol : Symbol
    {
        public TypeSymbol Type { get; protected set; }
        public ExpressionSyntax InitializerSyntax { get; private set; }
        
        public BoundExpression InitializerExpression { get; protected set; }

        protected FieldSymbol(IFieldSymbol sourceSymbol, AbstractPhaseContext bindContext)
            :base(sourceSymbol, bindContext)
        {
            ContainingType = bindContext.GetTypeSymbolWithoutRedirect(sourceSymbol.ContainingType);
            Type = bindContext.GetTypeSymbolWithoutRedirect(RoslynSymbol.Type);
        }

        public new IFieldSymbol RoslynSymbol => (IFieldSymbol)base.RoslynSymbol;

        public bool IsConst => RoslynSymbol.IsConst;
        public bool IsReadonly => RoslynSymbol.IsReadOnly;

        private bool _isBound;
        public override bool IsBound => _isBound;

        public bool IsSerialized
        {
            get
            {
                if (IsConst) return false;
                if (IsStatic) return false;
                if (RoslynSymbol.IsReadOnly) return false;
                if (HasAttribute<OdinSerializeAttribute>()) return true; // OdinSerializeAttribute takes precedence over NonSerializedAttribute
                if (HasAttribute<NonSerializedAttribute>()) return false;
                return RoslynSymbol.DeclaredAccessibility == Accessibility.Public || HasAttribute<SerializeField>() || HasAttribute<SerializeReference>();
            }
        }

        // There are better places this could go, but IsSerialized and this should stay in sync so we'll put them next to each other for visibility 
        internal static bool IsFieldSerialized(FieldInfo field)
        {
            if (field == null) return false;
            if (field.IsInitOnly) return false;
            if (field.IsStatic) return false;
            if (field.IsDefined(typeof(OdinSerializeAttribute), false)) return true;
            if (field.IsDefined(typeof(NonSerializedAttribute), false)) return true;
            return field.IsPublic || field.IsDefined(typeof(SerializeField), false) || field.IsDefined(typeof(SerializeReference), false);
        }

        public UdonSyncMode? SyncMode => GetAttribute<UdonSyncedAttribute>()?.NetworkSyncType;
        public bool IsSynced => SyncMode != null;

        private void CheckHiddenFields(BindContext context)
        {
            if (ContainingType.BaseType.IsExtern)
                return;

            TypeSymbol currentType = ContainingType.BaseType;

            while (!currentType.IsExtern)
            {
                FieldSymbol foundSymbol = currentType.GetMember<FieldSymbol>(Name, context);
                if (foundSymbol != null && !foundSymbol.IsConst)
                    throw new NotSupportedException("U# does not yet support hiding base fields");

                currentType = currentType.BaseType;
            }
        }

        public override void Bind(BindContext context)
        {
            if (IsBound)
                return;
            
            if (!RoslynSymbol.IsImplicitlyDeclared)
            {
                context.CurrentNode = RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax();
                InitializerSyntax = (context.CurrentNode as VariableDeclaratorSyntax)?.Initializer?.Value;
            }

            if (!IsExtern && IsStatic && !IsConst)
            {
                if (ContainingType.IsUdonSharpBehaviour)
                    throw new NotSupportedException("Static fields are not yet supported on user defined types");
                
                _isBound = true; // Non-UdonSharpBehaviour types can have static fields, declared it's just invalid to reference them. This avoids stuff where you have some utility class you have some constants or methods in that you want to use in part.
                return;
            }

            CheckHiddenFields(context);
            
            SetupAttributes(context);
            
            // Make sure the type is bound
            context.MarkSymbolReferenced(Type);
            
            // If this field is a forwarded type, it's possible that it's only declared and never actually referenced. 
            // In which case if we don't reference it here, the type info will not be generated for it and the serializer will throw warnings
            //  because at a surface level U# still only treats it as the non-forwarded type.
            if (!IsExtern && TypeSymbol.TryGetSystemType(Type, out Type systemType))
            {
                Type forwardedType = UdonSharpUtils.GetForwardedType(systemType);
                        
                if (forwardedType != systemType)
                {
                    while (forwardedType.IsArray)
                        forwardedType = forwardedType.GetElementType();
                                
                    TypeSymbol forwardedTypeSymbol = context.GetTypeSymbolWithoutRedirect(forwardedType);
                                
                    context.MarkSymbolReferenced(forwardedTypeSymbol);
                    context.CompileContext.AddReferencedUserType(forwardedTypeSymbol);
                }
            }

            _isBound = true;
        }
    }
}
