# Bucket.Ecs

![logo](logo.png)

Just another Ecs with its own pros and cons.

Idea behind Bucket.Ecs is to combine fast iterations from archetype ecs frameworks with fast migrations from non-archetype ecs frameworks.

The perfect case, that Bucket.Ecs is writing for - is to simulate lots and lots of entities, without overcomplicating your source code.

---
> [!WARNING]
Bucket.Ecs is not finished, API may change. \
Bucket.Ecs was tested only inside unity environment.

## Usage & Installation

git package: `git@github.com:RFS-6ro/Bucket.Ecs.git`

# How it works:

## World

Many ecs frameworks are using different worlds to optimize filters and memory usage. That is not the case for Bucket.Ecs.

In Bucket.Ecs you can define only one `EcsWorld` for the project. Filters and Queries are not influenced by the amount of entities in world or other, unrelevant archetypes.

## EntityId

Is just an `ulong` ID, used to identify entities. You cannot access entity data by this id.

> [!WARNING]
Bucket.Ecs does not yet support access to entity data outside of `EcsFilter` / `EcsQuery`. This is planned to be added in future versions.


## 🧩 Components

Bucket.Ecs combines archetype and sparse set components. Adding dynamic component to entity will not result in archetype migration.

```csharp
unsafe struct ArchetypeComponent : IEcsUnmanagedComponent
{
    public int* Data;
}

struct DynamicComponent : IEcsComponent
{
    public Transform Value;
}
```

Bucket.Ecs also supports shared and tag components.

```csharp
struct TagComponent : IEcsTagComponent { }

struct SharedComponent : IEcsSharedComponent
{
    public int value;
}
```

## Systems

Bucket.Ecs has different types of systems - for MainThread and for ThreadPool.

Inside of ThreadPool systems you can access only to archetype components, defined in the filter.

### Chunk system

You can access both archetype and sparse set components in this system.
``` csharp
class MainThreadCustomSystem : SystemBase
{
    public override void Init() { }

    public override void Run(in double deltaTime) { }

    public override void Dispose() { }
}

var systems = new EcsSystems(world);
systems.Group<UpdateGroup>().Add<MainThreadCustomSystem>();
```

### Chunk system

Iterates for each chunk of every archetype that matches the filter.
``` csharp
struct CustomMultiThreadSystem : IChunkSystem
{
    public CommandsScheduler CommandsScheduler { get; set; }

    public void GetFilterMask(EcsUnmanagedFilter.Mask mask) { }

    public void Run(in double deltaTime, in UnmanagedChunkData chunkData) { }
}

var systems = new EcsSystems(world);
systems.Group<UpdateGroup>()
    .ThreadPoolScope(scope =>
    {
        scope.AddChunkSystem<CustomMultiThreadSystem>();
    });
```

### ForEach system

Iterates for each entity that matches the filter.
``` csharp
struct MultiThreadForEachSystem : IForEachSystem
{
    public CommandsScheduler CommandsScheduler { get; set; }

    public void GetFilterMask(EcsUnmanagedFilter.Mask mask) { }

    public void Run(in double deltaTime, short entityIndex, in UnmanagedChunkData chunkData) { }
}

var systems = new EcsSystems(world);
systems.Group<UpdateGroup>()
    .ThreadPoolScope(scope =>
    {
        scope.AddForEachSystem<MultiThreadForEachSystem>();
    });
```

### Simple System

Previous ThreadPool systems will not be scheduled if no entity matches the filter. Using this system you can schedule work on the thread pool without entities.

> [!WARNING]
This type of systems is not yet supported

``` csharp
struct MultiThreadSystem : ISystem
{
    public CommandsScheduler CommandsScheduler { get; set; }

    public void Run(in double deltaTime) { }
}

var systems = new EcsSystems(world);
systems.Group<UpdateGroup>()
    .ThreadPoolScope(scope =>
    {
        scope.AddSystem<MultiThreadForEachSystem>();
    });
```

## Filters

### Filter and Component Access in MainThread

Component access on entities should be performed through `EcsQuery`. 
`EcsFilter` could be used for scenarios, where access to entity data is not required.

```csharp
EcsFilter filter = World.CreateFilter()
    .With<SparseSetComponent>
    .Without<SparseSetComponent>()
    .WithUnmanaged<ArchetypeComponent>()
    .WithoutUnmanaged<ArchetypeComponent>()
    .Build();

EcsQuery query = EcsQuery.WithFilter(filter);
foreach (var entityAddress in _query.ForEachEntity())
{
    ref var archetypeComponent = ref _query.GetUnmanagedRef<ArchetypeComponent>(entityAddress);
    
    ref var sparseSetComponent = ref _query.GetRef<SparseSetComponent>(entityAddress);
    
    bool hasComponent = _query.Has<SparseSetComponent>(entityAddress);
}
```
        
### Filter and Component Access in ThreadPool systems

`IChunkSystem` and `IForEachSystem` can have only one filter per system.

Filters in these systems can operate only with archetype components.

System should define in filter - which components it will read or write.

Adding read or write access to components will exclude entities without these components from filter.

```csharp
public void GetFilterMask(EcsUnmanagedFilter.Mask mask)
{
    mask
        .With<ArchetypeComponent>()
        .Without<ArchetypeComponent>()
        .ReadOnly<ArchetypeComponent>()
        .ReadWrite<ArchetypeComponent>();
}
```

When ThreadPool system receives a chunk - there's a guarantee that it will have fixed amount of entities inside.

Access components on entity by index in chunkData

```csharp
public void Run(in double deltaTime, in UnmanagedChunkData chunkData)
{
    for (short entityIndex = 0; entityIndex < chunkData.Count; entityIndex++)
    {
        EntityId entity = chunkData.GetEntityId(entityIndex);
        var component1 = chunkData.Read<ArchetypeComponent>(entityIndex);
        ref var component2 = ref chunkData.Ref<ArchetypeComponent>(entityIndex);
        chunkData.Write(entityIndex, new ArchetypeComponent() { data = 200 });
    }
}
```

---

# Features:

## Entity Builders

Because Bucket.Ecs is closer to an archetype-based solution - each migration takes a lot of CPU resources.

Using entity builder you can create an entity prototype, that will create entity in the target archetype, without migrations.

``` csharp
EcsWorld world = new EcsWorld();
EntityBuilder builder = _world.GetEntityBuilder();

using var eb = world.GetEntityBuilder()
    .WithUnmanaged<UnmanagedComponent>()
    .With<ComponentInBuilder>()
    .Build();

for (int i = 0; i < EntitiesCount; i++)
{
    // Create entity as is
    eb.Create();

    // Or perform some additional work on it, like setting initial values to component, or adding components by some condition.
    eb.Create((entity) => entity.Add<Component>());
}
```

## Entity Id provider 

By default entity ids are created by incrementing previously generated id. You can define your own id factory by implementing the following interface:
``` csharp
public interface IEntityIdFactory
{
    ulong GetNewId();
    void Recycle(ulong id);
}

var world = new EcsWorld(new CustomEntityIdFactory());
```

## Execution groups

Bucket.Ecs has 3 built-in groups for `Update`, `FixedUpdate` and `LateUpdate`.

```csharp
var systems = new EcsSystems(world);
systems.Group<UpdateGroup>().Run(deltaTime);
systems.Group<FixedUpdateGroup>().Run(deltaTime);
systems.Group<LateUpdateGroup>().Run(deltaTime);
```

Or, these calls could be simplified:
```csharp
var systems = new EcsSystems(world);
systems.Update(deltaTime);
systems.FixedUpdate(deltaTime);
systems.LateUpdate(deltaTime);
```

## Split your systems in features

Define custom features to simplify bootstrap of your systems.

``` csharp
public class CustomFeature : IEcsFeature
{
    public void Register(EcsSystems ecsSystems)
    {
        ecsSystems.Group<UpdateGroup>().Add<MainThreadSystem>();
    }
}

var systems = new EcsSystems(world);
systems.AddFeature(new CustomFeature());
```

## Custom execution groups

Create custom groups and define when they should be executed

You can add systems to the same group from different places.
That is very useful when different features have systems with similar responsibility. (i.e. Input or Render systems)

``` csharp
public class CustomGroup : EcsSystemsGroupDescriptor { }

var systems = new EcsSystems(world);
systems.AddGroup<CustomGroup>();

systems.Group<CustomGroup>().Run(deltaTime);
```

## Nested groups

You can have nested groups.
``` csharp
var systems = new EcsSystems(world);
systems.Group<UpdateGroup>()
    .AddGroup<InputGroup>()
    .AddGroup<NetworkGroup>()
    .AddGroup<RenderGroup>();

systems.Group<FixedUpdateGroup>()
    .AddGroup<SimulationGroup>();
```

## In-place filtration

Some Ecs frameworks are indexing entities in filters. While it improves performance in small projects - for large amount of entities and systems it has a significant overhead.

Bucket.Ecs does index archetype ids, while filtering entities by sparse set components in-place.

## Systems context

In many cases having just entity data is not enough for system to run.

In ThreadPool systems you can't access any managed data.

Bucket.Ecs provides a way to register system with additional context, that could be used in multithread
``` csharp

struct Context : IMultiThreadSystemContext
{
    public int data;
}

struct SystemWithContext : IChunkSystem
{
    public void Run(in double deltaTime, in UnmanagedChunkData chunkData)
    {
        Context context = chunkData.GetSystemContext<Context>();
        if (context.data == 5) { /* ... */ }
    }
}

systems.Group<UpdateGroup>().ThreadPoolScope(scope =>
{
    // Define system with context
    scope.AddChunkSystem<SystemWithContext, Context>();
});

// Set context from MainThread
world.SetSystemContext(new Context() { data = 5 });
```

## Sync Points

Sync Points are essential for applying changes made in systems. Sync Points are optimizing archetypes layout, creating and destroying entities and running entity migration. All of these operations are heavy to execute.

Sync Points are always placed after main thread systems.

Add an extra Sync Point in ThreadPool systems graph to assert the changes made in one system are applied, before scheduling next batch of thread work
``` csharp
var systems = new EcsSystems(world);
systems.Group<UpdateGroup>()
    .ThreadPoolScope(scope =>
    {
        scope.AddChunkSystem<CreateEntitiesSystem>()
            .AddSyncPoint()
            .AddChunkSystem<ReadComponentsOnNewEntitiesSystem>();
    });
```

## Aspects

Bucket.Ecs supports creating aspects to reuse in filters.
``` csharp
struct TestAspect : IEcsAspect
{
    public void Define(EcsFilter.Mask mask)
    {
        mask.With<Component>().WithUnmanaged<UnmanagedComponent>();
    }
}

struct TestUnmanagedAspect : IEcsUnmanagedAspect
{
    public void Define(EcsUnmanagedFilter.Mask mask)
    {
        mask.With<UnmanagedComponent>();
    }
}

// Usage:
var filter = World.CreateFilter().WithAspect<TestAspect>().Build();

public void GetFilterMask(EcsUnmanagedFilter.Mask mask)
{
    mask.WithAspect<TestUnmanagedAspect>();
}
```

## Commands from ThreadPool

Bucket.Ecs allows users to create custom commands that could be scheduled from ThreadPool systems.

These commands will be executed on the next Sync Point

``` csharp
const int CommandId = 1234;
void CustomCommand(in EntityAddress entityAddress, void* data) { }

var world = new EcsWorld();
world.RegisterCommand(CustomCommand, CommandId);

struct ScheduleCommandSystem : IChunkSystem
{
    public CommandsScheduler CommandsScheduler { get; set; }

    public void Run(in double deltaTime, in UnmanagedChunkData chunkData)
    {
        CommandsScheduler.ScheduleCustom(CommandId, entityAddress: address, data: null);
    }
}
```

## Entities Migrations

In both MainThread and ThreadPool systems entity migration between archetypes can be scheduled.

Either from `EcsQuery`
``` csharp
query.DelUnmanaged<ArchetypeComponent>(entityAddress);
query.AddUnmanaged(entityAddress, new ArchetypeComponent() { data = 100 });
```
                    
Or when accessing entity in chunk from ThreadPool system
``` csharp
chunkData.Del<ArchetypeComponent>(entityIndex);
chunkData.Add(entityIndex, new ArchetypeComponent() { data = 200 });
```
                    
> ReadWrite permissions are required to delete component from entity.
                    
All the migrations are going to be applied in batches on the next Sync Point. 

> Sync Point is called after every MainThread system, after ThreadPoolScope execution, or when manually added.

> [!WARNING]
Use with caution /
EntityAddress becomes invalid after archetype migration. /
Prefer Dynamic components usage for components that are added and removed often. They will not make entity archetype migration

## Systems Priority

Override MainThread system priority to move it in execution order.
> [!WARNING]
Systems will be sorted within the group/execution step they are defined in
``` csharp
class MainThreadCustomSystem : SystemBase
{
    // 0 is default
    // < 0 to move it in front of the execution order
    // > 0 to push it to the end of the execution order
    public override int Priority => -1;
}
```

## Logger

Bucket.Ecs does throw exceptions, but some errors are just logged as errors.

To keep track of them - implement `IBucketLogger` interface and assign your custom logger to BLogger static class
``` csharp
BLogger.Logger = new CustomLogger();
```

## System delay by time or frame

Implement one of the following interfaces if you need to make system wait for some time or frames between calls. 

This functionality allows spreading logic between frames for systems, that may not be executed each frame
``` csharp
public interface ISpreadFramesSystem
{
    int DelayFrames { get; }
}

public interface ISpreadTimestampSystem
{
    double DelayTime { get; }
}
```

## Unsafe access

Bucket.Ecs by default will throw an error if system tries to access entity data outside of defined dependencies or filters.

However - some systems are expected to be running in isolation and expects to access any component from entity. Defining all used component in the filter can be overwhelming.

Use this call to allow system access any components without checks
``` csharp
public void Run(in double deltaTime, in UnmanagedChunkData chunkData)
{
    // To allow read any component
    chunkData.SetUnsafeAccessMode(UnmanagedChunkData.UnsafeAccessMode.AllowRead);

    // To allow read, write, add or del any component
    chunkData.SetUnsafeAccessMode(UnmanagedChunkData.UnsafeAccessMode.AllowWrite);
}
```

## Parallel VS SystemsGraph

When declaring the ThreadPool scope - you can switch between 2 types of schedulling - Sequential and DependencyGraph
``` csharp
var systems = new EcsSystems(world);
systems.Group<UpdateGroup>()
    .ThreadPoolScope(scope =>
    {
        scope
            .RespectOrder()
            // Systems will be executed in order they were defined
                .AddChunkSystem<System1>()
                .AddChunkSystem<System2>()

            .BuildGraph()
            // If System3 depends on component state written in System4 - System4 will be executed first
                .AddChunkSystem<System3>()
                .AddChunkSystem<System4>()
        ;
    });
```

> The final execution graph, defined here will be:

> System1, System2, SyncPoint, System4, System3

---

# 🏗️ Bootstrap Example

```csharp
public class Bootstrap : IDisposable
{
    private EcsWorld world;
    private EcsSystems systems;

    public Bootstrap()
    {
        world = new EcsWorld();
        systems = new EcsSystems(world);

        systems.Group<UpdateGroup>()
            .Add(new InitEntitiesSystem());

        systems.AddFeature(new CustomFeature());
        systems.Group<UpdateGroup>().AddGroup<CustomGroup>();

        using var builder = world.GetEntityBuilder()
            .WithUnmanaged<UnmanagedComponent>()
            .Build();

        builder.Create();

        systems.Init();
    }

    public void Update(in double deltaTime)
    {
        systems.Update(deltaTime);
        // or systems.Group<CustomGroup>().Run(deltaTime);
    }

    public void Dispose()
    {
        world.Dispose(); world = null;
        systems.Dispose(); systems = null;
    }
}
```

---

# Roadmap & plans:

- Plugins
- Unity inspector support
- Optimization & New features 
- Entity pin to access components without filter
- Tests
- Platforms & IL2CPP support