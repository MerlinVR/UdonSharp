
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Core;
using NotSupportedException = UdonSharp.Core.NotSupportedException;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundFieldAccessExpression : BoundAccessExpression
    {
        protected BoundFieldAccessExpression(SyntaxNode node, BoundExpression sourceExpression)
            : base(node, sourceExpression)
        {
        }

        public static BoundAccessExpression BindFieldAccess(AbstractPhaseContext context, SyntaxNode node, FieldSymbol fieldSymbol, BoundExpression sourceExpression)
        {
            if (fieldSymbol.IsConst)
                return new BoundConstantExpression(fieldSymbol.RoslynSymbol.ConstantValue, fieldSymbol.Type);
            
            // if (TryCreateShimFieldAccess(context, node, fieldSymbol, sourceExpression, out BoundFieldAccessExpression createdFieldAccess))
            //     return createdFieldAccess;

            if (fieldSymbol.IsStatic && !fieldSymbol.IsExtern)
            {
                throw new NotSupportedException("Static fields are not yet supported on user defined types");
            }
            
            switch (fieldSymbol)
            {
                case ExternFieldSymbol externField:
                    return new BoundExternFieldAccessExpression(node, externField, sourceExpression);
                case UdonSharpBehaviourFieldSymbol udonSharpBehaviourFieldSymbol:
                    return new BoundUdonSharpBehaviourFieldAccessExpression(node, udonSharpBehaviourFieldSymbol, sourceExpression);
                case ImportedUdonSharpFieldSymbol importedUdonSharpFieldSymbol:
                    return new BoundImportedTypeInstanceFieldAccessExpression(context, node, importedUdonSharpFieldSymbol, sourceExpression);
            }
            
            throw new NotImplementedException($"Field access for symbol {fieldSymbol.GetType()} is not implemented");
        }

        // private static bool TryCreateShimFieldAccess(AbstractPhaseContext context, SyntaxNode node, FieldSymbol symbol, BoundExpression instanceExpression, out BoundFieldAccessExpression createdFieldAccess)
        // {
        //     if (symbol.ContainingType.IsGenericType && symbol.ContainingType.ToString().StartsWith("System.Collections.Generic.KeyValuePair", StringComparison.Ordinal))
        //     {
        //         TypeSymbol.TryGetSystemType(symbol.ContainingType, out Type keyValueType);
        //         Type[] genericTypes = keyValueType.GetGenericArguments();
        //         TypeSymbol keyValueShim = context.GetTypeSymbol(typeof(Lib.Internal.Collections.DictionaryKeyValue<,>).MakeGenericType(genericTypes));
        //
        //         FieldSymbol field = keyValueShim.GetMember<FieldSymbol>(symbol.Name, context);
        //
        //         if (field != null)
        //         {
        //             createdFieldAccess = new BoundImportedTypeInstanceFieldAccessExpression(context, node, field, instanceExpression);
        //             return true;
        //         }
        //     }
        //     
        //     createdFieldAccess = null;
        //     return false;
        // }

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

        private sealed class BoundExternFieldAccessExpression : BoundFieldAccessExpression
        {
            private ExternFieldSymbol Field { get; }

            public override TypeSymbol ValueType => Field.Type;

            public BoundExternFieldAccessExpression(SyntaxNode node, ExternFieldSymbol field, BoundExpression sourceExpression) 
                : base(node, sourceExpression)
            {
                Field = field;
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
                    })), Field.Type);
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

        private sealed class BoundImportedTypeInstanceFieldAccessExpression : BoundFieldAccessExpression
        {
            private ImportedUdonSharpFieldSymbol Field { get; }
            public override TypeSymbol ValueType => Field.Type;

            public BoundImportedTypeInstanceFieldAccessExpression(AbstractPhaseContext context, SyntaxNode node, FieldSymbol fieldSymbol, BoundExpression sourceExpression) 
                : base(node, sourceExpression)
            {
                Field = (ImportedUdonSharpFieldSymbol)fieldSymbol;
            }

            private Value.CowValue GetFieldInstanceValue(EmitContext context)
            {
                Value.CowValue thisRef;

                if (SourceExpression?.IsThis ?? true)
                {
                    Value.CowValue[] instanceValue = context.GetExpressionCowValues(this, "propertyInstance");

                    if (instanceValue != null)
                        return instanceValue[0];
                    
                    thisRef = context.GetInstanceValue().GetCowValue(context);
                    
                    context.RegisterCowValues(new []{thisRef}, this, "propertyInstance");
                }
                else
                {
                    thisRef = GetInstanceValue(context);
                }
                
                return thisRef;
            }
            
            public override Value EmitValue(EmitContext context)
            {
                Value.CowValue thisRef = GetFieldInstanceValue(context);
                
                BoundAccessExpression objectArrayAccess = new BoundArrayAccessExpression(SyntaxNode, context,
                    BindAccess(thisRef),
                    new BoundExpression[]
                    {
                        BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), Field.FieldIndex))
                    },
                    context.GetTypeSymbol(SpecialType.System_Object));

                if (!UdonSharpUtils.IsStrongBoxedType(Field.Type.UdonType.SystemType))
                {
                    return context.CastValue(context.EmitValue(objectArrayAccess), ValueType);
                }

                Value convertedArrayValue = context.CastValue(context.EmitValue(objectArrayAccess), Field.Type.UdonType.MakeArrayType(context));
                    
                BoundAccessExpression strongBoxAccess = BindElementAccess(context, SyntaxNode,
                    BindAccess(convertedArrayValue),
                    new BoundExpression[]
                    {
                        BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 0))
                    });
                
                return context.CastValue(context.EmitValue(strongBoxAccess), Field.Type);
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                Value.CowValue thisRef = GetFieldInstanceValue(context);
                
                BoundAccessExpression objectArrayAccess = new BoundArrayAccessExpression(SyntaxNode, context,
                    BindAccess(thisRef),
                    new BoundExpression[]
                    {
                        BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), Field.FieldIndex)),
                    },
                    context.GetTypeSymbol(SpecialType.System_Object));
                
                Value value = context.EmitValue(valueExpression);
                
                if (UdonSharpUtils.IsStrongBoxedType(Field.Type.UdonType.SystemType))
                {
                    BoundAccessExpression strongBoxAccess = BindElementAccess(context, SyntaxNode,
                        BindAccess(context.CastValue(context.EmitValue(objectArrayAccess), Field.Type.UdonType.MakeArrayType(context))),
                        new BoundExpression[]
                        {
                            BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 0))
                        });
                    
                    context.EmitSet(strongBoxAccess, BindAccess(context.CastValue(value, Field.Type)));
                    
                    return value;
                }
                
                context.EmitSet(objectArrayAccess, BindAccess(context.CastValue(value, ValueType)));
                
                return value;
            }
        }
    }
}
