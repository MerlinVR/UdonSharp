using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    /// <summary>
    /// The base type for all U# symbols
    /// </summary>
    internal abstract class Symbol
    {
        /// <summary>
        /// The symbol this is declared in, for instance if this is a field symbol, will point to the declaring class symbol, or if a local variable symbol, will point to a method symbol
        /// </summary>
        public virtual Symbol ContainingSymbol { get; protected set; }

        /// <summary>
        /// Used to retrieve the non-generic-typed version of this symbol, for instance if you're getting a symbol for DoThing<int>(), will return DoThing<>()
        /// </summary>
        public virtual Symbol OriginalSymbol { get { return null; } }

        /// <summary>
        /// The source Roslyn-generated symbol for this U# symbol
        /// </summary>
        public virtual ISymbol RoslynSymbol { get; protected set; }

        /// <summary>
        /// If this symbol has had its body visited and types linked
        /// </summary>
        public virtual bool IsBound { get { return true; } }

        /// <summary>
        /// If this is a symbol pointing to an Udon extern
        /// </summary>
        public virtual bool IsExtern { get { return false; } }

        /// <summary>
        /// If this is a static symbol. This may return true on fields, properties, and methods. Classes will only return true on this if they are marked as a static class.
        /// </summary>
        public virtual bool IsStatic { get { return false; } }

        /// <summary>
        /// Gets direct dependencies of this symbol, this symbol must be resolved for the dependencies to be valid
        /// Will throw exception if this symbol has not been resolved
        /// </summary>
        /// <returns></returns>
        public abstract ImmutableArray<Symbol> GetDirectDependencies();

        /// <summary>
        /// Gets direct dependencies of this symbol, this symbol must be resolved for the dependencies to be valid
        /// Will throw exception if this symbol has not been resolved
        /// </summary>
        /// <returns></returns>
        public ImmutableArray<Symbol> GetDirectDependencies<T>() where T : Symbol
        {
            return ImmutableArray.CreateRange<Symbol>(GetDirectDependencies().OfType<T>()); // Todo: cache
        }

        ImmutableArray<Symbol> _lazyAllDependencies;
        readonly object dependencyFindLock = new object();

        private bool AllDependenciesResolved { get { return _lazyAllDependencies != null; } }
        
        protected Symbol(ISymbol sourceSymbol, BindContext bindContext)
        {
            RoslynSymbol = sourceSymbol;

            if (sourceSymbol.ContainingType != null)
                ContainingSymbol = bindContext.GetSymbol(sourceSymbol.ContainingType);
        }

        /// <summary>
        /// Tries to get the dependencies from the cached dependencies of a symbol. If they have not been cached, searches in place on this method to prevent deadlocks where two symbols may depend on eachother while they have not had their dependencies resolved
        /// </summary>
        /// <param name="searchDependencies"></param>
        /// <param name="resolvedDependencies"></param>
        /// <returns></returns>
        private IEnumerable<Symbol> GetAllDependenciesRecursive(IEnumerable<Symbol> searchDependencies, HashSet<Symbol> resolvedDependencies = null)
        {
            Queue<Symbol> workingSet = new Queue<Symbol>(searchDependencies.Distinct());

            if (resolvedDependencies == null)
                resolvedDependencies = new HashSet<Symbol>();

            while (workingSet.Count > 0)
            {
                Symbol currentSymbol = workingSet.Dequeue();

                if (!currentSymbol.IsBound)
                    throw new System.InvalidOperationException("Cannot gather dependencies from unresolved symbol");

                if (currentSymbol == this || resolvedDependencies.Contains(currentSymbol))
                    continue;

                resolvedDependencies.Add(currentSymbol);

                if (currentSymbol.AllDependenciesResolved)
                {
                    resolvedDependencies.UnionWith(currentSymbol.GetAllDependencies());
                    continue;
                }

                resolvedDependencies.UnionWith(GetAllDependenciesRecursive(currentSymbol.GetDirectDependencies(), resolvedDependencies));
            }

            return resolvedDependencies;
        }

        /// <summary>
        /// Gets all dependencies of this symbol recursively. This will only be valid after the bind phase has finished. 
        /// If called before binding has finished, dependencies will not necessarily be fully resolved for this symbol and this will throw an exception in that case.
        /// </summary>
        /// <returns></returns>
        public ImmutableArray<Symbol> GetAllDependencies()
        {
            if (_lazyAllDependencies != null)
                return _lazyAllDependencies;

            lock (dependencyFindLock)
            {
                if (_lazyAllDependencies != null)
                    return _lazyAllDependencies;

                ImmutableArray<Symbol> directDependencies = GetDirectDependencies();
                
                _lazyAllDependencies = ImmutableArray.CreateRange(GetAllDependenciesRecursive(directDependencies));
            }

            return _lazyAllDependencies;
        }

        public ImmutableArray<Symbol> GetAllDependencies<T>() where T : Symbol
        { 
            // If we end up using this frequently and not just for tests, this should be optimized to cache the arrays of the types
            return ImmutableArray.CreateRange<Symbol>(GetAllDependencies().OfType<T>());
        }

        /// <summary>
        /// Returns all symbols that are an implementation of this symbol.
        /// This means interfaces will collect all the used implementations of the interface, methods will collect all overrides of the method, properties will collect all overrides, etc.
        /// </summary>
        /// <returns></returns>
        public virtual ImmutableArray<Symbol> GetImplementations() => ImmutableArray<Symbol>.Empty;

        public abstract void Bind(BindContext context);

        // UdonSharp symbols will always have a Roslyn analogue symbol that refers to exactly the same data, the only difference is that UdonSharp symbols will annotate more information and track dependencies more explicitly
        // So we just use the root symbol hash code and equals here
        // This is not needed most of the time since you must retrieve the symbols from the Roslyn symbols in most contexts that matter for comparison. The retrieval mechanisms already guarantee that there is one UdonSharp symbol for a given Roslyn symbol
        public override int GetHashCode()
        {
            return RoslynSymbol.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return RoslynSymbol.Equals(obj);
        }

        // This may get more descriptive info in the future
        public override string ToString()
        {
            return RoslynSymbol.ToString();
        }
    }
}
