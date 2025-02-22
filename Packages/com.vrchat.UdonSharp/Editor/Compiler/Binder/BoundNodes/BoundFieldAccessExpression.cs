
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Core;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundFieldAccessExpression : BoundAccessExpression
    {
        protected BoundFieldAccessExpression(SyntaxNode node, BoundExpression sourceExpression)
            : base(node, sourceExpression)
        {
        }

        public static BoundAccessExpression BindFieldAccess(SyntaxNode node, FieldSymbol fieldSymbol, BoundExpression sourceExpression)
        {
            if (fieldSymbol.IsConst)
                return new BoundConstantExpression(fieldSymbol.RoslynSymbol.ConstantValue, fieldSymbol.Type);
            
            if (fieldSymbol is ExternFieldSymbol externField)
            {
                return new BoundExternFieldAccessExpression(node, externField, sourceExpression);
            }

            if (fieldSymbol is UdonSharpBehaviourFieldSymbol udonSharpBehaviourFieldSymbol)
                return new BoundUdonSharpBehaviourFieldAccessExpression(node, udonSharpBehaviourFieldSymbol,
                    sourceExpression);

            throw new NotImplementedException($"Field access for symbol {fieldSymbol.GetType()} is not implemented");
        }

        private sealed class BoundExternFieldAccessExpression : BoundFieldAccessExpression
        {
            private ExternFieldSymbol Field { get; }

            public override TypeSymbol ValueType => Field.Type;

            public BoundExternFieldAccessExpression(SyntaxNode node, ExternFieldSymbol field, BoundExpression sourceExpression) 
                : base(node, sourceExpression)
            {
                Field = field;
            }

            private Value.CowValue GetInstanceValue(EmitContext context)
            {
                Value.CowValue[] instanceValue = context.GetExpressionCowValues(this, "propertyInstance");

                if (instanceValue != null)
                    return instanceValue[0];
                
                Value.CowValue instanceCowValue;
                
                using (context.InterruptAssignmentScope())
                    instanceCowValue = context.EmitValueWithDeferredRelease(SourceExpression).GetCowValue(context);
                
                context.RegisterCowValues(new []{instanceCowValue}, this, "propertyInstance");

                return instanceCowValue;
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                Value.CowValue sourceValue = null;

                if (!Field.IsStatic)
                    sourceValue = GetInstanceValue(context);
                
                Value expressionResult = context.EmitValue(valueExpression);
                
                if (sourceValue != null)
                    context.Module.AddPush(sourceValue.Value);

                context.Module.AddPush(expressionResult);

                context.Module.AddExternSet(Field);

                if (sourceValue != null)
                {
                    if (SourceExpression.ValueType.IsValueType &&
                        SourceExpression is BoundAccessExpression sourceAccessExpression)
                        context.EmitSet(sourceAccessExpression, BindAccess(sourceValue.Value));
                }

                return expressionResult;
            }

            public override Value EmitValue(EmitContext context)
            {
                Value returnValue = context.GetReturnValue(Field.Type);
                
                Value.CowValue sourceValue = null;
                
                if (!Field.IsStatic)
                    sourceValue = GetInstanceValue(context);
                
                if (sourceValue != null)
                    context.Module.AddPush(sourceValue.Value);
                
                context.Module.AddPush(returnValue);
                
                context.Module.AddExternGet(Field);

                return returnValue;
            }
        }

        private sealed class BoundUdonSharpBehaviourFieldAccessExpression : BoundFieldAccessExpression
        {
            private FieldSymbol Field { get; }
            
            public BoundUdonSharpBehaviourFieldAccessExpression(SyntaxNode node, FieldSymbol fieldSymbol, BoundExpression sourceExpression) 
                : base(node, sourceExpression)
            {
                Field = fieldSymbol;
            }

            public override TypeSymbol ValueType => Field.Type;

            public override Value EmitValue(EmitContext context)
            {
                if (SourceExpression == null || SourceExpression.IsThis)
                    return context.GetUserValue(Field);
                
                TypeSymbol stringType = context.GetTypeSymbol(SpecialType.System_String);
                MethodSymbol setProgramVariableMethod = context.GetTypeSymbol(typeof(UdonSharpBehaviour))
                    .GetMembers<MethodSymbol>("GetProgramVariable", context)
                    .First(e => e.Parameters.Length == 1 && 
                                e.Parameters[0].Type == stringType);

                return context.CastValue(context.EmitValue(BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode,
                    setProgramVariableMethod, SourceExpression,
                    new BoundExpression[]
                    {
                        BindAccess(context.GetConstantValue(stringType, Field.Name))
                    })), Field.Type, true);
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                if (SourceExpression == null || SourceExpression.IsThis)
                    return context.EmitValueAssignment(context.GetUserValue(Field), valueExpression);

                if (Field.HasAttribute<FieldChangeCallbackAttribute>())
                    throw new CompilerException("Cannot set field on U# behaviour by reference when that field has a FieldChangeCallback attribute.");
                
                TypeSymbol stringType = context.GetTypeSymbol(SpecialType.System_String);
                MethodSymbol setProgramVariableMethod = context.GetTypeSymbol(typeof(UdonSharpBehaviour))
                    .GetMembers<MethodSymbol>("SetProgramVariable", context)
                    .First(e => e.Parameters.Length == 2 && 
                                e.Parameters[0].Type == stringType);

                Value value = context.EmitValue(valueExpression);

                context.Emit(BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode,
                    setProgramVariableMethod, SourceExpression,
                    new BoundExpression[]
                    {
                        BindAccess(context.GetConstantValue(stringType, Field.Name)),
                        BindAccess(value)
                    }));

                return value;
            }
        }
    }
}
