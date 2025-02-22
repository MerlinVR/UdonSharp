
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Symbols
{
    internal class ImportedUdonSharpMethodSymbol : MethodSymbol
    {
        public ImportedUdonSharpMethodSymbol(IMethodSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
        }

        public override void Emit(EmitContext context)
        {
            // We only run the logic following this for constructors because they must set all fields to their default values before running the constructor body
            if (!IsConstructor)
            {
                base.Emit(context);
                return;
            }
            
            // For constructors we need to first go through each field initializer and run them in order with readonly fields first, then normal fields.
            // If fields do not have initializers we need to initialize them to their default values if they are value types, otherwise they will be null initialized by the object array constructor.
            EmitContext.MethodLinkage linkage = context.GetMethodLinkage(this, false);
            
            context.Module.AddCommentTag("");
            context.Module.AddCommentTag(RoslynSymbol.ToDisplayString().RemoveNewLines());
            context.Module.AddCommentTag("");
            context.Module.LabelJump(linkage.MethodLabel);
            
            using (context.OpenBlockScope())
            {
                // First set the ID of the type in the object array, we don't bother strongboxing this because it's a single value that will never change
                ulong typeID = (ulong)Internal.UdonSharpInternalUtility.GetTypeID(TypeSymbol.GetFullTypeName(ContainingType.RoslynSymbol));
                
                context.EmitSet(BoundAccessExpression.BindElementAccess(context, null,
                    BoundAccessExpression.BindAccess(context.GetInstanceValue()),
                    new BoundExpression[]
                    {
                        BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), ImportedUdonSharpTypeSymbol.HEADER_TYPE_ID_INDEX)),
                    }),
                    BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_UInt64), typeID)));
                
                // Next we need to go through fields and run their initializers, readonly fields first as per C# spec
                FieldSymbol[] fields = ContainingType.FieldSymbols.Where(e => !e.IsConst && !e.IsStatic).OrderByDescending(e => e.IsReadonly).ToArray();

                foreach (FieldSymbol field in fields)
                {
                    if (field.InitializerExpression != null)
                    {
                        context.Module.AddCommentTag($"{field.Name} = {field.InitializerExpression.SyntaxNode.ToFullString().RemoveNewLines().TrimLength(60, true)}");
                        
                        Value value = context.EmitValue(field.InitializerExpression);
                        
                        int fieldIndex = ((ImportedUdonSharpFieldSymbol)field).FieldIndex;
                    
                        BoundAccessExpression objectArrayAccess = BoundAccessExpression.BindElementAccess(context, null,
                            BoundAccessExpression.BindAccess(context.GetInstanceValue()),
                            new BoundExpression[]
                            {
                                BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), fieldIndex)),
                            });

                        if (UdonSharpUtils.IsStrongBoxedType(field.Type.UdonType.SystemType))
                        {
                            // First initialize the array as a 1-sized array of the type and set it to the value
                            Value strongBox = context.EmitValue(new BoundArrayCreationExpression(null, context,
                                    field.Type.UdonType.MakeArrayType(context),
                                    new BoundExpression[]
                                    {
                                        BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 1))
                                    }, 
                                    new BoundExpression[] { BoundAccessExpression.BindAccess(context.CastValue(value, field.Type)) }));
                            
                            // Set the strongbox into the object array
                            context.EmitSet(objectArrayAccess, BoundAccessExpression.BindAccess(strongBox));
                        }
                        else
                        {
                            context.EmitSet(objectArrayAccess, BoundAccessExpression.BindAccess(context.CastValue(value, field.Type)));
                        }
                    }
                    else if (field.Type.IsValueType)
                    {
                        BoundAccessExpression objectArrayAccess = BoundAccessExpression.BindElementAccess(context, null,
                            BoundAccessExpression.BindAccess(context.GetInstanceValue()),
                            new BoundExpression[]
                            {
                                BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), ((ImportedUdonSharpFieldSymbol)field).FieldIndex)),
                            });
                        
                        if (UdonSharpUtils.IsStrongBoxedType(field.Type.UdonType.SystemType))
                        {
                            // First initialize the array as a 1-sized array of the type and set it to the default value
                            Value strongBox = context.EmitValue(new BoundArrayCreationExpression(null, context,
                                    field.Type.UdonType.MakeArrayType(context),
                                    new BoundExpression[]
                                    {
                                        BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 1))
                                    }, 
                                    new BoundExpression[] { BoundAccessExpression.BindAccess(context.GetConstantValue(field.Type, Activator.CreateInstance(field.Type.UdonType.SystemType))) }));
                            
                            // Set the strongbox into the object array
                            context.EmitSet(objectArrayAccess, BoundAccessExpression.BindAccess(strongBox));
                        }
                        else
                        {
                            context.EmitSet(objectArrayAccess, BoundAccessExpression.BindAccess(context.GetConstantValue(field.Type, Activator.CreateInstance(field.Type.UdonType.SystemType))));
                        }
                    }
                }
                
                // We then need to go though properties and run their initializers
                foreach (ImportedUdonSharpPropertySymbol property in ContainingType.GetMembers<ImportedUdonSharpPropertySymbol>(context))
                {
                    if (property.InitializerExpression != null)
                    {
                        context.Module.AddCommentTag($"{property.Name} = {property.InitializerExpression.SyntaxNode.ToFullString().RemoveNewLines().TrimLength(60, true)}");
                        
                        Value value = context.EmitValue(property.InitializerExpression);

                        // Get field symbol for auto property backing field
                        ImportedUdonSharpFieldSymbol fieldSymbol = (ImportedUdonSharpFieldSymbol)property.BackingField;
                        
                        BoundAccessExpression objectArrayAccess = BoundAccessExpression.BindElementAccess(context, null,
                            BoundAccessExpression.BindAccess(context.GetInstanceValue()),
                            new BoundExpression[]
                            {
                                BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), fieldSymbol.FieldIndex)),
                            });
                        
                        if (UdonSharpUtils.IsStrongBoxedType(property.Type.UdonType.SystemType))
                        {
                            // First initialize the array as a 1-sized array of the type
                            Value strongBox = context.EmitValue(new BoundArrayCreationExpression(null, context,
                                    property.Type.UdonType.MakeArrayType(context),
                                    new BoundExpression[]
                                    {
                                        BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 1))
                                    }, 
                                    new BoundExpression[] { BoundAccessExpression.BindAccess(context.CastValue(value, property.Type)) }));
                            
                            // Set the strongbox into the object array
                            context.EmitSet(objectArrayAccess, BoundAccessExpression.BindAccess(strongBox));
                        }
                        else
                        {
                            context.EmitSet(objectArrayAccess, BoundAccessExpression.BindAccess(context.CastValue(value, property.Type)));
                        }
                    }
                }

                if (MethodBody is BoundExpression bodyExpression)
                {
                    context.EmitReturn(bodyExpression);
                }
                else if (MethodBody != null)
                {
                    context.Emit(MethodBody);
                    context.EmitReturn();
                }
                else
                {
                    context.EmitReturn();
                }

                context.FlattenTableCounters();
            }
        }
    }
}
