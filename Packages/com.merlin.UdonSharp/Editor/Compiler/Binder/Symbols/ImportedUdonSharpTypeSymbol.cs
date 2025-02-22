
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;
using NotSupportedException = UdonSharp.Core.NotSupportedException;

namespace UdonSharp.Compiler.Symbols
{
    internal sealed class ImportedUdonSharpTypeSymbol : TypeSymbol
    {
        /// <summary>
        /// The header size of the object array for user types, contains the ID of the type as uint64 at the moment
        /// </summary>
        internal const int HEADER_SIZE = 1;
        
        public const int HEADER_TYPE_ID_INDEX = 0;

        public override int UserTypeAllocationSize => FieldSymbols.Count(e => !e.IsStatic && !e.IsConst) + HEADER_SIZE;
        
        private List<FieldSymbol> _memberFieldSymbols;

        public ImportedUdonSharpTypeSymbol(INamedTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            UdonType = sourceSymbol.TypeKind == TypeKind.Enum ? context.GetTypeSymbol(sourceSymbol.EnumUnderlyingType).UdonType : context.GetTypeSymbol(typeof(object[])).UdonType;
        }
        
        public ImportedUdonSharpTypeSymbol(IArrayTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol.ElementType.TypeKind == TypeKind.Enum)
            {
                UdonType = (ExternTypeSymbol)context.GetTypeSymbol(((INamedTypeSymbol) sourceSymbol.ElementType).EnumUnderlyingType).UdonType.MakeArrayType(context);
            }
            else
            {
                UdonType = context.GetTypeSymbol(typeof(object[])).UdonType;
            }
        }

        public override void Bind(BindContext context)
        {
            if (IsBound)
                return;
            
            base.Bind(context);
            
            if (!FieldSymbols.IsDefault)
                _memberFieldSymbols = FieldSymbols.Where(e => !e.IsConst && !e.IsStatic).ToList();
            
            if (IsArray)
                return;
            
            if (RoslynSymbol.IsValueType && RoslynSymbol.TypeKind != TypeKind.Enum)
                throw new NotSupportedException("Structs are not supported for user types, consider using class", RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation());
            
            if (RoslynSymbol.TypeKind == TypeKind.Interface)
                throw new NotSupportedException("Interfaces are not supported by U#", RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation());
            
            if (RoslynSymbol.IsAbstract)
                throw new NotSupportedException("Abstract classes are not supported by U#", RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation());
            
            if (!IsValueType && (BaseType != context.GetTypeSymbol(typeof(object))))
                throw new NotSupportedException("Inheritance is not supported by U# on non-UdonSharpBehaviour classes", RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation());
            
            foreach (ISymbol symbol in RoslynSymbol.GetMembers())
            {
                if (symbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Destructor && !methodSymbol.IsImplicitlyDeclared)
                    throw new NotSupportedException("Destructors on user classes are not supported by U#.", methodSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation());
            }
            
            // Get U# Property Symbols and make them get bound so they have valid data when we're checking if they have initializer expressions
            foreach (PropertySymbol propertySymbol in GetMembers<PropertySymbol>(context))
            {
                context.MarkSymbolReferenced(propertySymbol);
            }
            
            context.CompileContext.AddReferencedUserType(this);
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, AbstractPhaseContext context)
        {
            switch (roslynSymbol)
            {
                case null:
                    throw new NullReferenceException("Source symbol cannot be null");
                case IMethodSymbol methodSymbol:
                    return MakeMethodSymbol(methodSymbol, context);
                case IFieldSymbol fieldSymbol:
                    return new ImportedUdonSharpFieldSymbol(fieldSymbol, context);
                case ILocalSymbol localSymbol:
                    return new LocalSymbol(localSymbol, context);
                case IParameterSymbol parameterSymbol:
                    return new ParameterSymbol(parameterSymbol, context);
                case IPropertySymbol propertySymbol:
                    return new ImportedUdonSharpPropertySymbol(propertySymbol, context);
                case ITypeSymbol typeSymbol:
                    throw new NotSupportedException("Nested type declarations are not currently supported by U#", typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation());
            }
            
            throw new System.NotImplementedException($"Handling for Symbol {roslynSymbol} is not implemented");
        }

        private static Symbol MakeMethodSymbol(IMethodSymbol methodSymbol, AbstractPhaseContext context)
        {
            if (methodSymbol.MethodKind == MethodKind.BuiltinOperator &&
                methodSymbol.Parameters.Length == 2 && 
                methodSymbol.Parameters[0].Type.TypeKind == TypeKind.Enum &&
                context.GetTypeSymbol(methodSymbol.ReturnType) == context.GetTypeSymbol(SpecialType.System_Boolean))
            {
                TypeSymbol parameterType = context.GetTypeSymbol(methodSymbol.Parameters[0].Type);

                BuiltinOperatorType operatorType;
                if (methodSymbol.Name == "op_Equality")
                    operatorType = BuiltinOperatorType.Equality;
                else
                    operatorType = BuiltinOperatorType.Inequality;
                
                return new ExternSynthesizedOperatorSymbol(operatorType, parameterType.UdonType, context);
            }

            return new ImportedUdonSharpMethodSymbol(methodSymbol, context);
        }

        public override int GetUserFieldIndex(FieldSymbol field)
        {
            if (!IsBound)
                throw new InvalidOperationException("Type symbol must be bound to get user field index");
            
            if (field.ContainingType != this)
                throw new ArgumentException($"Field Symbol {field} is not member of type symbol {this}", nameof(field));
            
            // UdonSharpUtils.Log($"Field index for {field} of type {field.Type} in {this} is {_memberFieldSymbols.IndexOf(field)}");
            
            if (_memberFieldSymbols == null)
                throw new InvalidOperationException($"Cannot get field index for field {field}, member field symbols are not initialized on type {this}");
            
            int fieldIndex = _memberFieldSymbols.IndexOf(field);
            
            if (fieldIndex == -1)
                throw new ArgumentException($"Could not get field index {field} for type {this}", nameof(field));
            
            return fieldIndex + HEADER_SIZE;
        }
    }
}
