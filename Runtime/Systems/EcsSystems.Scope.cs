using System.Collections.Generic;

namespace Bucket.Ecs.v3
{
#if UNITY
    using AllowUnsafePtr = Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute;
    using WriteAccess = Unity.Collections.LowLevel.Unsafe.WriteAccessRequiredAttribute;
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;

    public sealed partial class EcsSystems
    {
        public System.IDisposable ParallelScope => new Scope.Parallel(this);
        public System.IDisposable MainThreadScope => new Scope.MainThread(this);
        public System.IDisposable DependencyGraphScope => new Scope.UnorderedDependencyGraph(this);

        private Scope.MainThread _rootScope;
        private Stack<Scope.ScopeType> _scopes;

        [Inline(256)]
        private void InitScope()
        {
            _scopes = new Stack<Scope.ScopeType>();
            _rootScope = new Scope.MainThread(this);
        }

        [Inline(256)]
        public void PushScope(Scope.ScopeType scopeType)
        {
            _scopes.Push(scopeType);
        }

        [Inline(256)]
        public Scope.ScopeType GetCurrentScope()
        {
            return _scopes.Peek();
        }

        [Inline(256)]
        public Scope.ScopeType PopScope()
        {
            return _scopes.Pop();
        }

        [Inline(256)]
        private void DisposeScope()
        {
            _rootScope.Dispose();
            BAssert.IsEmpty(_scopes.Count, "Scopes were not closed.");
            _scopes = null;
        }
    }
    
    public sealed class Scope
    {
        public enum ScopeType
        {
            /// <summary>
            /// Executes in main thread, even if the system is defined as MultiThread
            /// </summary>
            MainThread,
            /// <summary>
            /// Executes in registration order with each chunk in parallel
            /// </summary>
            Parallel,
            /// <summary>
            /// Executes the most optimized order of systems
            /// </summary>
            UnorderedDependencyGraph
        }
        
        public sealed class MainThread : ScopeBase
        {
            private readonly EcsSystems _systems;
            
            public MainThread(EcsSystems systems)
            {
                _systems = systems;
                _systems.PushScope(ScopeType.MainThread);
            }

            [Inline(256)]
            protected override void CloseScope()
            {
                BAssert.True(ScopeType.MainThread == _systems.PopScope(), "Scopes don't match");
            }
        }

        public sealed class Parallel : ScopeBase
        {
            private readonly EcsSystems _systems;
            
            public Parallel(EcsSystems systems)
            {
                _systems = systems;
                _systems.PushScope(ScopeType.Parallel);
            }

            [Inline(256)]
            protected override void CloseScope()
            {
                BAssert.True(ScopeType.Parallel == _systems.PopScope(), "Scopes don't match");
            }
        }

        public sealed class UnorderedDependencyGraph : ScopeBase
        {
            private readonly EcsSystems _systems;
            
            public UnorderedDependencyGraph(EcsSystems systems)
            {
                _systems = systems;
                _systems.PushScope(ScopeType.UnorderedDependencyGraph);
            }

            [Inline(256)]
            protected override void CloseScope()
            {
                BAssert.True(ScopeType.UnorderedDependencyGraph == _systems.PopScope(), "Scopes don't match");
            }
        }

        public abstract class ScopeBase : System.IDisposable
        {
            private bool _disposed;

            [Inline(256)]
            internal virtual void Dispose(bool disposing)
            {
                if (_disposed == false)
                {
                    if (disposing) CloseScope();

                    _disposed = true;
                }
            }

            [Inline(256)]
            ~ScopeBase()
            {
                if (_disposed == false)
                {
                    BExceptionThrower.ObjectNotDisposed(GetType().Name + " was not disposed! You should use the 'using' keyword or manually call Dispose.");
                }

                Dispose(disposing: false);
            }

            [Inline(256)]
            public void Dispose()
            {
                Dispose(disposing: true);
                System.GC.SuppressFinalize(this);
            }

            [Inline(256)]
            protected abstract void CloseScope();
        }
    }
}
