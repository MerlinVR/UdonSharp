
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundExpression : BoundNode
    {
        public bool IsConstant => ConstantValue != null;
        public virtual IConstantValue ConstantValue => null;

        /// <summary>
        /// The user type of Value that this expression will emit when EmitValue is called
        /// </summary>
        public abstract TypeSymbol ValueType { get; }
        
        public bool IsThis { get; protected set; }

        /// <summary>
        /// The expression that gets evaluated to get the value of this expression,
        /// for example a field access expression would have a source expression of the instance expression; with `a.b` the source expression would be `a`
        /// On the other hand an assignment expression `a = b` would have a source expression of `b`
        /// </summary>
        protected BoundExpression SourceExpression { get; }

        protected BoundExpression(SyntaxNode node, BoundExpression sourceExpression = null)
            : base(node)
        {
            SourceExpression = sourceExpression;
        }

        /// <summary>
        /// All expressions must instead implement EmitValue since they will always evaluate to something
        /// </summary>
        /// <param name="context"></param>
        public override void Emit(EmitContext context)
        {
            context.EmitValue(this);
        }

        public abstract Value EmitValue(EmitContext context);
        
        protected virtual void ReleaseCowValuesImpl(EmitContext context) {}
        
        public void ReleaseCowReferences(EmitContext context)
        {
            ReleaseCowValuesImpl(context);
            context.ReleaseCowValues(this);
            SourceExpression?.ReleaseCowReferences(context);
        }
    }

    internal class BoundConstantExpression : BoundAccessExpression
    {
        public override IConstantValue ConstantValue { get; }

        public TypeSymbol ConstantType { get; }

        public override TypeSymbol ValueType => ConstantType;

        public BoundConstantExpression(IConstantValue constantValue, TypeSymbol constantType, SyntaxNode node)
            :base(node, null)
        {
            ConstantValue = constantValue;
            ConstantType = constantType;
        }

        public BoundConstantExpression(object constantValue, TypeSymbol typeSymbol, SyntaxNode node = null)
            :base(node, null)
        {
            ConstantType = typeSymbol;

            Type targetType = typeSymbol.UdonType.SystemType;

            if (typeSymbol.IsEnum && typeSymbol.IsExtern)
                constantValue = Enum.ToObject(targetType, constantValue);
            
            ConstantValue =
                (IConstantValue) Activator.CreateInstance(typeof(ConstantValue<>).MakeGenericType(typeSymbol.UdonType.SystemType),
                    constantValue);
        }

        public override Value EmitValue(EmitContext context)
        {
            return context.GetConstantValue(ConstantType, ConstantValue.Value);
        }

        public override string ToString()
        {
            return $"BoundConstantExpression<{ConstantValue.GetType().GetGenericArguments()[0]}>: " + ConstantValue.Value?.ToString() ?? "null";
        }

        public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
        {
            throw new InvalidOperationException("Cannot set value on a constant value");
        }
    }
}
