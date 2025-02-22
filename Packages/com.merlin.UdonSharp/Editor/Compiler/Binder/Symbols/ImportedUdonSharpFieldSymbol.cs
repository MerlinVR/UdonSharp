
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal class ImportedUdonSharpFieldSymbol : FieldSymbol
    {
        private int _userFieldIdx = -1;

        /// <summary>
        /// Index into object[] array for the field storage.
        /// </summary>
        public int FieldIndex
        {
            get
            {
                if (_userFieldIdx == -1)
                    _userFieldIdx = ContainingType.GetUserFieldIndex(this);

                return _userFieldIdx;
            }
        }

        public ImportedUdonSharpFieldSymbol(IFieldSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
            base.Bind(context);

            // Only bind the initializer on UdonSharp types that are imported aka not UdonSharpBehaviour but plain classes
            // We don't want to parse U# behaviour initializers because they are allowed to do things that are not allowed in Udon
            if (InitializerSyntax != null)
            {
                BinderSyntaxVisitor bodyVisitor = new BinderSyntaxVisitor(this, context);
                InitializerExpression = bodyVisitor.VisitVariableInitializer(InitializerSyntax, Type);
            }
        }
    }
}
