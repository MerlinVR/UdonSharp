
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal class ImportedUdonSharpPropertySymbol : PropertySymbol
    {
        public ImportedUdonSharpPropertySymbol(IPropertySymbol sourceSymbol, AbstractPhaseContext context)
            :base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
            base.Bind(context);
            
            context.CurrentNode = RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax();
            InitializerSyntax = (context.CurrentNode as PropertyDeclarationSyntax)?.Initializer?.Value;
            
            if (InitializerSyntax != null)
            {
                BinderSyntaxVisitor bodyVisitor = new BinderSyntaxVisitor(this, context);
                InitializerExpression = bodyVisitor.VisitVariableInitializer(InitializerSyntax, Type);
            }
        }
    }
}
