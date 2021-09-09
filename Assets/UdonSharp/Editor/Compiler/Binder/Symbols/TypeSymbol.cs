
using System;
using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal abstract class TypeSymbol : Symbol
    {
        private static readonly object dictionaryLazyInitLock = new object();
        private ConcurrentDictionary<ISymbol, Symbol> _typeSymbols;

        public new ITypeSymbol RoslynSymbol => (ITypeSymbol)base.RoslynSymbol;

        public bool IsValueType => RoslynSymbol.IsValueType;

        public bool IsArray => RoslynSymbol.TypeKind == TypeKind.Array;

        public bool IsEnum => RoslynSymbol.TypeKind == TypeKind.Enum;

        public bool IsUdonSharpBehaviour => !IsArray && ((INamedTypeSymbol) RoslynSymbol).IsUdonSharpBehaviour();

        public ExternTypeSymbol UdonType { get; protected set; }

        private TypeSymbol _elementType;
        public TypeSymbol ElementType
        {
            get
            {
                if (!IsArray)
                    throw new InvalidOperationException("Cannot get element type on non-array types");

                return _elementType;
            }
            protected set => _elementType = value;
        }
        
        public TypeSymbol BaseType { get; }

        protected TypeSymbol(ISymbol sourceSymbol, AbstractPhaseContext bindContext)
            : base(sourceSymbol, bindContext)
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            if (RoslynSymbol.BaseType != null && !IsExtern) // We don't use the base type on extern types and if we bind the base here, it can cause loops due to how Udon maps types
                BaseType = bindContext.GetTypeSymbol(RoslynSymbol.BaseType);
            
            if (IsArray)
                ElementType = bindContext.GetTypeSymbol(((IArrayTypeSymbol)sourceSymbol).ElementType);
        }

        private void InitSymbolDict()
        {
            if (_typeSymbols != null)
                return;

            lock (dictionaryLazyInitLock)
            {
                if (_typeSymbols != null)
                    return;

                _typeSymbols = new ConcurrentDictionary<ISymbol, Symbol>();
            }
        }

        private bool _bound;

        public override bool IsBound => _bound;

        public override void Bind(BindContext context)
        {
            if (_bound)
                return;

            var members = RoslynSymbol.GetMembers();

            foreach (var member in members.Where(member => !member.IsImplicitlyDeclared))
            {
                switch (member)
                {
                    case IFieldSymbol _:
                    case IPropertySymbol _:
                    case IMethodSymbol methodSymbol when methodSymbol.MethodKind != MethodKind.PropertyGet && methodSymbol.MethodKind != MethodKind.PropertySet:
                        Symbol boundSymbol = context.GetSymbol(member);
                        
                        if (!boundSymbol.IsBound)
                            using (context.OpenMemberBindScope(boundSymbol))
                                boundSymbol.Bind(context);

                        boundSymbol.SetAttributes(SetupAttributes(context, boundSymbol));
                        
                        break;
                }
            }
            
            SetAttributes(SetupAttributes(context, this));

            _bound = true;
        }

        public IEnumerable<TypeSymbol> CollectReferencedUnboundTypes(AbstractPhaseContext context)
        {
            HashSet<TypeSymbol> referencedTypes = new HashSet<TypeSymbol>();

            foreach (Symbol member in GetMembers(context))
            {
                if (member.DirectDependencies == null)
                    continue;
                
                referencedTypes.UnionWith(member.DirectDependencies.Where(e => !e.IsBound).OfType<TypeSymbol>()
                    .Where(e => !e.IsArray));
            }

            if (BaseType != null && !BaseType.IsBound)
                referencedTypes.Add(BaseType);

            return referencedTypes;
        }

        public Symbol GetMember(ISymbol symbol, AbstractPhaseContext context)
        {
            InitSymbolDict();

            // Extension method handling
            if (symbol is IMethodSymbol methodSymbol &&
                methodSymbol.IsExtensionMethod &&
                methodSymbol.ReducedFrom != null)
                symbol = methodSymbol.ReducedFrom;
            
            return _typeSymbols.GetOrAdd(symbol, (key) => CreateSymbol(symbol, context));
        }

        public T GetMember<T>(ISymbol symbol, AbstractPhaseContext context) where T : Symbol
        {
            return (T)GetMember(symbol, context);
        }

        public IEnumerable<T> GetMembers<T>(AbstractPhaseContext context) where T : Symbol
        {
            return GetMembers(context).OfType<T>();
        }

        public IEnumerable<Symbol> GetMembers(AbstractPhaseContext context)
        {
            List<Symbol> symbols = new List<Symbol>();
            
            foreach (ISymbol member in RoslynSymbol.GetMembers())
            {
                symbols.Add(GetMember(member, context));
            }

            return symbols;
        }

        public IEnumerable<Symbol> GetMembers(string name, AbstractPhaseContext context)
        {
            List<Symbol> symbols = new List<Symbol>();
            
            foreach (ISymbol member in RoslynSymbol.GetMembers(name))
            {
                symbols.Add(GetMember(member, context));
            }

            return symbols;
        }
        
        public IEnumerable<T> GetMembers<T>(string name, AbstractPhaseContext context) where T : Symbol
        {
            return GetMembers(name, context).OfType<T>();
        }

        public Symbol GetMember(string name, AbstractPhaseContext context)
        {
            return GetMember(RoslynSymbol.GetMembers(name).First(), context);
        }
        
        public T GetMember<T>(string name, AbstractPhaseContext context) where T : Symbol
        {
            return GetMembers<T>(name, context).FirstOrDefault();
        }

        public TypeSymbol MakeArrayType(AbstractPhaseContext context)
        {
            return context.GetTypeSymbol(context.CompileContext.RoslynCompilation.CreateArrayTypeSymbol(RoslynSymbol));
        }

        private Type _cachedType;
        private static readonly SymbolDisplayFormat _fullTypeFormat =
            new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private static readonly System.Reflection.Assembly _gameScriptAssembly =
            AppDomain.CurrentDomain.GetAssemblies().First(e => e.GetName().Name == "Assembly-CSharp");

        public static string GetFullTypeName(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(_fullTypeFormat);
        }
        
        public bool TryGetSystemType(out Type systemType)
        {
            if (_cachedType != null)
            {
                systemType = _cachedType;
                return true;
            }

            if (IsExtern)
            {
                _cachedType = systemType = ((ExternTypeSymbol) this).SystemType;
                return true;
            }

            int arrayDepth = 0;
            TypeSymbol currentType = this;
            while (currentType.IsArray)
            {
                arrayDepth++;
                currentType = currentType.ElementType;
            }
            
            string typeName = GetFullTypeName(currentType.RoslynSymbol);

            Type foundType = _gameScriptAssembly.GetType(typeName);

            if (foundType != null)
            {
                while (arrayDepth > 0)
                {
                    arrayDepth--;
                    foundType = foundType.MakeArrayType();
                }
                
                _cachedType = systemType = foundType;
                return true;
            }

            systemType = null;
            return false;
        }

        protected static ImmutableArray<Attribute> SetupAttributes(BindContext context, Symbol symbol)
        {
            var attribData = symbol.RoslynSymbol.GetAttributes();
            Attribute[] attributes = new Attribute[attribData.Length];

            for (int i = 0; i < attributes.Length; ++i)
            {
                AttributeData attribute = attribData[i];
                
                TypeSymbol type = context.GetTypeSymbol(attribute.AttributeClass);

                object[] attributeArgs = new object[attribute.ConstructorArguments.Length];

                for (int j = 0; j < attributeArgs.Length; ++j)
                {
                    object attribValue;

                    var constructorArg = attribute.ConstructorArguments[j];
                    
                    if (constructorArg.Type != null &&
                        constructorArg.Type.TypeKind == TypeKind.Enum)
                    {
                        TypeSymbol typeSymbol = context.GetTypeSymbol(constructorArg.Type);
                        attribValue = Enum.ToObject(typeSymbol.UdonType.SystemType, constructorArg.Value);
                    }
                    else
                    {
                        attribValue = attribute.ConstructorArguments[j].Value;
                    }

                    attributeArgs[j] = attribValue;
                }

                attributes[i] = (Attribute)Activator.CreateInstance(type.UdonType.SystemType, attributeArgs);
            }

            return attributes.ToImmutableArray();
        }

        /// <summary>
        /// Implemented by derived type symbols to create their own relevant symbol for the roslyn symbol
        /// </summary>
        /// <param name="roslynSymbol"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract Symbol CreateSymbol(ISymbol roslynSymbol, AbstractPhaseContext context);
    }
}
