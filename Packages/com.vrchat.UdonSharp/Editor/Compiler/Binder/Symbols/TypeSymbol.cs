
using System;
using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Symbols
{
    internal abstract class TypeSymbol : Symbol
    {
        private readonly object _dictionaryLazyInitLock = new object();
        private ConcurrentDictionary<ISymbol, Symbol> _typeSymbols;
        
        /// <summary>
        /// Maps A method symbol to its locals.
        /// This is needed to prevent conflicts of local symbols between instances of a generic method with different type arguments.
        /// </summary>
        private ConcurrentDictionary<IMethodSymbol, Dictionary<ILocalSymbol, LocalSymbol>> _methodLocalSymbols;

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
        public ImmutableArray<TypeSymbol> TypeArguments { get; }

        protected TypeSymbol(ISymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            if (RoslynSymbol.BaseType != null && !IsExtern) // We don't use the base type on extern types and if we bind the base here, it can cause loops due to how Udon maps types
                BaseType = context.GetTypeSymbol(RoslynSymbol.BaseType);
            
            if (IsArray)
                ElementType = context.GetTypeSymbol(((IArrayTypeSymbol)sourceSymbol).ElementType);

            if (sourceSymbol is INamedTypeSymbol sourceNamedType)
            {
                TypeArguments = sourceNamedType.TypeArguments.Length > 0
                    ? sourceNamedType.TypeArguments.Select(context.GetTypeSymbol).ToImmutableArray()
                    : ImmutableArray<TypeSymbol>.Empty;

                if (RoslynSymbol.OriginalDefinition != RoslynSymbol)
                    OriginalSymbol = context.GetSymbol(RoslynSymbol.OriginalDefinition);
                else
                    OriginalSymbol = this;
            }
            else
            {
                TypeArguments = ImmutableArray<TypeSymbol>.Empty;
            }
        }

        private void InitSymbolDict()
        {
            if (_typeSymbols != null)
                return;

            lock (_dictionaryLazyInitLock)
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

            if (IsArray)
            {
                _bound = true;
                return;
            }

            if (TypeArguments.Length > 0 && this == OriginalSymbol)
            {
                _bound = true;
                return;
            }
            
            context.CurrentNode = RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax();

            if (IsUdonSharpBehaviour)
            {
                if (RoslynSymbol.AllInterfaces.Length > 2) // Be lazy and ignore the serialization callback receiver since this is temporary
                    throw new NotImplementedException("Interfaces are not yet handled by U#");
                
                SetupAttributes(context);
            }

            ImmutableArray<ISymbol> members = RoslynSymbol.GetMembers();

            foreach (ISymbol member in members.Where(member => (!member.IsImplicitlyDeclared || member.Kind == SymbolKind.Field)))
            {
                switch (member)
                {
                    case IFieldSymbol _:
                    case IPropertySymbol property when !property.IsStatic && IsUdonSharpBehaviour:
                    case IMethodSymbol method when !method.IsStatic && IsUdonSharpBehaviour:
                        Symbol boundSymbol = context.GetSymbol(member);
                        
                        if (!boundSymbol.IsBound)
                            using (context.OpenMemberBindScope(boundSymbol))
                                boundSymbol.Bind(context);
                        
                        break;
                }
            }

            _bound = true;
        }

        public Dictionary<TypeSymbol, HashSet<Symbol>> CollectReferencedUnboundSymbols(BindContext context, IEnumerable<Symbol> extraBindMembers)
        {
            Dictionary<TypeSymbol, HashSet<Symbol>> referencedTypes = new Dictionary<TypeSymbol, HashSet<Symbol>>();

            IEnumerable<Symbol> allMembers = GetMembers(context).Concat(extraBindMembers);

            foreach (Symbol member in allMembers)
            {
                if (member.DirectDependencies == null)
                    continue;

                foreach (Symbol dependency in member.DirectDependencies.Where(e => !e.IsBound))
                {
                    if (dependency is TypeSymbol typeSymbol)
                    {
                        if (!referencedTypes.ContainsKey(typeSymbol))
                            referencedTypes.Add(typeSymbol, new HashSet<Symbol>());
                    }
                    else
                    {
                        TypeSymbol containingType = dependency.ContainingType;
                        if (!referencedTypes.ContainsKey(containingType))
                            referencedTypes.Add(containingType, new HashSet<Symbol>());

                        referencedTypes[containingType].Add(dependency);
                    }
                }
            }

            if (BaseType != null && !BaseType.IsBound && 
                !referencedTypes.ContainsKey(BaseType))
                referencedTypes.Add(BaseType, new HashSet<Symbol>());

            if (IsArray)
            {
                TypeSymbol currentSymbol = ElementType;
                while (currentSymbol.IsArray)
                    currentSymbol = currentSymbol.ElementType;
                
                if (!referencedTypes.ContainsKey(currentSymbol))
                    referencedTypes.Add(currentSymbol, new HashSet<Symbol>());
            }

            return referencedTypes;
        }

        public Symbol GetMember(ISymbol symbol, AbstractPhaseContext context)
        {
            InitSymbolDict();

            // Extension method handling
            if (symbol is IMethodSymbol methodSymbol &&
                methodSymbol.IsExtensionMethod &&
                methodSymbol.ReducedFrom != null)
            {
                symbol = methodSymbol.ReducedFrom;

                if (methodSymbol.IsGenericMethod)
                    symbol = ((IMethodSymbol)symbol).Construct(methodSymbol.TypeArguments.ToArray());
            }

            // Treats symbols as local to a particular method symbol across different method type arguments
            // Prevents LocalSymbol info from leaking across multiple uses of the same method with different generic type arguments
            if (symbol is ILocalSymbol localSymbol)
            {
                MethodSymbol currentBindMethod = ((BindContext)context).CurrentBindMethod;

                if (_methodLocalSymbols == null)
                {
                    lock (_dictionaryLazyInitLock)
                    {
                        if (_methodLocalSymbols == null)
                        {
                            _methodLocalSymbols = new ConcurrentDictionary<IMethodSymbol, Dictionary<ILocalSymbol, LocalSymbol>>();
                        }
                    }
                }
                
                Dictionary<ILocalSymbol, LocalSymbol> localMap = _methodLocalSymbols.GetOrAdd(currentBindMethod.RoslynSymbol, (key) => new Dictionary<ILocalSymbol, LocalSymbol>());

                if (localMap.TryGetValue(localSymbol, out LocalSymbol foundSymbol))
                    return foundSymbol;

                LocalSymbol newLocal = (LocalSymbol)CreateSymbol(symbol, context);
                
                localMap.Add(localSymbol, newLocal);

                return newLocal;
            }

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

        /// <summary>
        /// Implemented by derived type symbols to create their own relevant symbol for the roslyn symbol
        /// </summary>
        /// <param name="roslynSymbol"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract Symbol CreateSymbol(ISymbol roslynSymbol, AbstractPhaseContext context);

        private Type _cachedType;
        private static readonly SymbolDisplayFormat _fullTypeFormat =
            new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

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

            if (TryGetSystemType(RoslynSymbol, out systemType))
            {
                _cachedType = systemType;
                return true;
            }

            return false;
        }
        
        private static readonly SymbolDisplayFormat _externFullTypeFormat =
            new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
        private static readonly SymbolDisplayFormat _externTypeFormat =
            new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly);
        
        private static Type MakeGenericTypeInternal(Type baseType, INamedTypeSymbol typeSymbol)
        {
            if (baseType == null)
                return null;

            if (typeSymbol.IsGenericType != baseType.IsGenericType)
                return null;
            
            if (!typeSymbol.IsGenericType && !baseType.IsGenericType)
                return baseType;
                
            Type[] typeArguments = new Type[typeSymbol.TypeArguments.Length];

            for (int i = 0; i < typeArguments.Length; ++i)
            {
                if (typeSymbol.TypeArguments[i].TypeKind == TypeKind.TypeParameter)
                    return baseType;
                
                if (!TryGetSystemType(typeSymbol.TypeArguments[i], out var typeArgument))
                    return null;

                typeArguments[i] = typeArgument;
            }

            Type constructedType;

            try
            {
                constructedType = baseType.MakeGenericType(typeArguments);
            }
            catch (Exception) // Some type constraint may have changed and cause the MakeGenericType to fail
            {
                return null;
            }

            return constructedType;
        }
        
        private static Type GetSystemTypeInternal(string typeName, INamedTypeSymbol typeSymbol)
        {
            var containingAssembly = typeSymbol.GetExternAssembly();

            if (containingAssembly != null)
            {
                return MakeGenericTypeInternal(containingAssembly.GetType(typeName), typeSymbol);
            }

            foreach (var udonSharpAssembly in CompilerUdonInterface.UdonSharpAssemblies)
            {
                Type foundType = udonSharpAssembly.GetType(typeName);

                if (foundType != null)
                    return MakeGenericTypeInternal(foundType, typeSymbol);
            }

            return null;
        }
        
        public static bool TryGetSystemType(ITypeSymbol typeSymbol, out Type systemType)
        {
            systemType = null;
            
            Stack<int> arrayRanks = null;

            if (typeSymbol.TypeKind == TypeKind.Array)
            {
                arrayRanks = new Stack<int>();

                ITypeSymbol currentType = typeSymbol;
                while (currentType.TypeKind == TypeKind.Array)
                {
                    IArrayTypeSymbol currentArrayType = (IArrayTypeSymbol)currentType;
                    arrayRanks.Push(currentArrayType.Rank);
                    
                    currentType = currentArrayType.ElementType;
                }

                typeSymbol = currentType;
            }

            INamedTypeSymbol namedType = (INamedTypeSymbol)typeSymbol;
            
            Stack<INamedTypeSymbol> containingTypeStack = null;

            if (namedType.ContainingType != null)
            {
                containingTypeStack = new Stack<INamedTypeSymbol>();
                while (namedType != null)
                {
                    containingTypeStack.Push(namedType);
                    namedType = namedType.ContainingType;
                }

                namedType = containingTypeStack.Peek();
            }

            Type foundType;

            if (containingTypeStack == null || 
                containingTypeStack.Count == 1)
            {
                string typeName = typeSymbol.ToDisplayString(_externFullTypeFormat);
            
                if (namedType.IsGenericType)
                    typeName += $"`{namedType.TypeArguments.Length}";

                foundType = GetSystemTypeInternal(typeName, namedType);

                if (foundType == null)
                    return false;
            }
            else
            {
                INamedTypeSymbol rootTypeSymbol = containingTypeStack.Pop();
                string rootTypeName = rootTypeSymbol.ToDisplayString(_externFullTypeFormat);

                if (rootTypeSymbol.IsGenericType)
                    rootTypeName += $"`{rootTypeSymbol.TypeArguments.Length}";
                
                Type rootType = GetSystemTypeInternal(rootTypeName, namedType);
                
                if (rootType == null)
                    return false;
                
                Type currentFoundType = rootType;

                while (containingTypeStack.Count > 0)
                {
                    INamedTypeSymbol currentNamedType = containingTypeStack.Pop();
                    string currentTypeName = currentNamedType.ToDisplayString(_externTypeFormat);
                    if (currentNamedType.IsGenericType)
                        currentTypeName += $"`{currentNamedType.TypeArguments.Length}";
                        
                    currentFoundType = MakeGenericTypeInternal(currentFoundType.GetNestedType(currentTypeName), currentNamedType);

                    if (currentFoundType == null)
                        return false;
                }

                foundType = currentFoundType;
            }

            if (arrayRanks != null)
            {
                while (arrayRanks.Count > 0)
                {
                    int rank = arrayRanks.Pop();

                    // .MakeArrayType() and .MakeArrayType(1) do not return the same thing
                    // See remarks in https://docs.microsoft.com/en-us/dotnet/api/system.type.makearraytype?view=net-5.0#System_Type_MakeArrayType_System_Int32_
                    foundType = rank == 1 ? foundType.MakeArrayType() : foundType.MakeArrayType(rank);
                }
            }

            systemType = foundType;
            return true;
        }
    }
}
