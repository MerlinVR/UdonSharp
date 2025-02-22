
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
        
        public BoundExpression InitializerExpression { get; private set; }

        protected FieldSymbol(IFieldSymbol sourceSymbol, AbstractPhaseContext bindContext)
            :base(sourceSymbol, bindContext)
        {
            ContainingType = bindContext.GetTypeSymbol(sourceSymbol.ContainingType);
            Type = bindContext.GetTypeSymbol(RoslynSymbol.Type);
        }

        public new IFieldSymbol RoslynSymbol => (IFieldSymbol)base.RoslynSymbol;

        public bool IsConst => RoslynSymbol.IsConst;
        public bool IsReadonly => RoslynSymbol.IsReadOnly;

        private bool _resolved;
        public override bool IsBound => _resolved;

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
            if (field.IsInitOnly) return false;
            if (field.IsStatic) return false;
            if (field.IsDefined(typeof(OdinSerializeAttribute), false)) return true;
            if (field.IsDefined(typeof(NonSerializedAttribute), false)) return true;
            return field.IsPublic || field.IsDefined(typeof(SerializeField), false) || field.IsDefined(typeof(SerializeReference), false);
        }

        public bool IsConstInitialized => InitializerExpression != null && InitializerExpression.IsConstant;

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
                throw new NotSupportedException("Static fields are not yet supported on user defined types");
            
            CheckHiddenFields(context);
            
            SetupAttributes(context);
            
            // Re-get the type symbol to register it as a dependency in the bind context
            TypeSymbol fieldType = context.GetTypeSymbol(RoslynSymbol.Type);
            // Type fieldSystemType = fieldType.UdonType.SystemType;

            // if (InitializerSyntax != null && 
            //     (!HasAttribute<CompileInitAttribute>() && 
            //      fieldSystemType != typeof(VRCUrl) && 
            //      fieldSystemType != typeof(VRCUrl[])))
            // {
            //     BinderSyntaxVisitor bodyVisitor = new BinderSyntaxVisitor(this, context);
            //     InitializerExpression = bodyVisitor.VisitVariableInitializer(InitializerSyntax, fieldType);
            // }

            _resolved = true;
        }
    }
}
