
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Binder;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;

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
                if (IsStatic) return false;
                if (RoslynSymbol.IsReadOnly) return false;
                if (HasAttribute<OdinSerializeAttribute>()) return true; // OdinSerializeAttribute takes precedence over NonSerializedAttribute
                if (HasAttribute<NonSerializedAttribute>()) return false;
                return RoslynSymbol.DeclaredAccessibility == Accessibility.Public || HasAttribute<SerializeField>();
            }
        }

        public bool IsConstInitialized => InitializerExpression != null && InitializerExpression.IsConstant;

        public UdonSyncMode? SyncMode => GetAttribute<UdonSyncedAttribute>()?.NetworkSyncType;
        public bool IsSynced => SyncMode != null;

        public override void Bind(BindContext context)
        {
            if (IsBound)
                return;
            
            if (!RoslynSymbol.IsImplicitlyDeclared)
                InitializerSyntax = (RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax() as VariableDeclaratorSyntax)?.Initializer?.Value;
            
            // Re-get the type symbol to register it as a dependency in the bind context
            TypeSymbol fieldType = context.GetTypeSymbol(RoslynSymbol.Type);

            if (InitializerSyntax != null)
            {
                BinderSyntaxVisitor bodyVisitor = new BinderSyntaxVisitor(this, context);
                InitializerExpression = bodyVisitor.VisitExpression(InitializerSyntax, fieldType);
            }
            
            SetupAttributes(context);

            _resolved = true;
        }
    }
}
