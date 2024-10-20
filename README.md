# Bucket.Ecs

Just another Ecs with its own pros and cons.

Idea behind Bucket Ecs is to combine fast iterations from archetype ecs frameworks with fast migrations from non-archetype ecs frameworks.

The perfect case, that Bucket Ecs is writing for - is to simulate lots and lots of entities, without overcomplicating your source code.

---
> [!WARNING]
> Bucket Ecs is not finished, API may change.

## Usage & Installation

Bucket Ecs was tested only inside unity environment. 

# The concept:

## Worlds

A standard storage for entities and components.

## Entity
An entity is just an ID to work with a set of components. By itself it means nothing and you can't access, or save it somewhere.

There's no option to get an entity outside of the filter.

Entity can have 0 components attached. You need to manually delete it.

Entity is ulong - you should have more than enough range to play for a while.

## Repositories

All entities are inside of archetypes, called EntityRepositories.

They are split into chunks of 4096 items. Entities are not sorted in any way inside the chunks. They are taking the first found space.

If a repository is empty - it is going to be recycled and reused by need. The performance should not degrade in long sessions.

## Filters

The filter is an object that is linked to the system.

Each repository has an ID. The filter is a collection of their IDs, so it's a lightweight object. The performance should not decrease with the amount of new systems and filters.

## Components

Containers for data. They should be a type of struct and implement `IEcsComponent` interface.

``` c#
public struct Health : IEcsComponent
{
    public float value;
}
```

## Systems

A system is a class that defines a logic for entities that match the filter.
System should implement `IEcsSystem` interface.

System can have only one filter, and work with only one chunk of one entity repository at a time.

``` c#
public class FooSystem : IEcsSystem
{
    public void CreateFilter(ComponentBitMask mask)
    {
        mask
            .With<Health>()
            .Without<DeadTag>()
        ;
    }

    public void Init(EcsWorld world)
    {
    }

    public void Run(float deltaTime, in IterationContext context)
    {
        foreach (var entity in context)
        {
            
        }
    }
}
```

You need to register system in a container to run it:

``` c#
public class Boot : Monobehaviour
{
    private EcsWorld _world;
    private IEcsSystems _systems;
    private IEcsSystemsRunner _systemsRunner;

    private void Awake()
    {
        _world = new EcsWorld();
        _systems = new EcsSystems(_world);
        _systemsRunner = _systems;

        _systems
            .AddMainThread(new FooSystem())
        ;

        _systems.Init();
    }

    private void Update()
    {
        float deltaTime = UnityEngine.Time.deltaTime;
        _systemsRunner.Run(deltaTime);
    }
}
```

Systems are receiving a chunk of archetypes to iterate over. That means it's guaranteed that <U>all</U> entities in the chunk have the same set of components.

That's a small example of a system, where you can see all the APIs used to write the logic
``` c#
public class MovementSystem : IEcsSystem
{
    ... 

    public void Run(float deltaTime, in IterationContext context)
    {
        bool hasResultComponent = context.Has<Result>();

        var resultAccess = context.Access<Result>();
        var value1Access = context.Access<Value1>();
        var value2Access = context.Access<Value2>();

        foreach (var entityIndex in context)
        {
            ref readonly var value1 = ref value1Access.RO(entityIndex); //Read Only
            ref readonly var value2 = ref value2Access.RO(entityIndex); //Read Only

            if (hasResultComponent == false)
            {
                ref var result = ref context.Add<Acceleration>(entityIndex);
                result.value = value1 + value2;
            }
            else
            {
                ref var result = ref resultAccess.RW(entityIndex); //Read Write
                result.value = value1 + value2;
            }
        }
    }
}
```

---

# Features:

## Entity Builders

Because Bucket Ecs is closer to an archetype-based solution - each migration takes a lot of CPU resources.

And, by design, you can't access individual entities outside of filters - that means you can't add components outside of systems.

That's why there's an EntityBuilder object. You need to select what components should be added to the new entity.

Then you just call the "Create" method.

``` c#
EntityBuilder builder = _world.GetEntityBuilder();

builder
    .Add<CPosition>()
    .Add<CDirection>()
    .Add<CSpeed>()
    .Add<CView>()
;

builder.Create();
```

## World Events and conditional systems

There's always a situation where you need to disable or enable the system based on a condition or event. 

The system needs to implement one of the interfaces below.

``` c#
public interface IEcsSystemEnableIfEventIsRaised
{ 
    public int EnableEventId { get; }
}

public interface IEcsSystemDisableIfEventIsRaised
{ 
    public int DisableEventId { get; }
}
```

Then you can raise, check, or suspend an event using methods in the EcsWorld. You can call it if you have access to EcsWorld. From another system, UI, or trigger.

``` c#
EcsWorld world;
int eventId = 0;

world.RaiseEventSingleton(eventId);
bool v = world.HasEventSingleton(eventId);
world.SuspendEventSingleton(eventId);
```

---

## Roadmap & plans:

- Bucket Ecs was built with multi-threading in mind. But right now only single thread sequential iterations are implemented. Thread safe is not guaranteed. 
 
   It is already planned to create systems dependency graph and parallel iterations. I am working on adding a code generation to collect them. I am not planning to change the API much. Any system written before the thread safe update should work the same way, but faster.

- Currently, Bucket Ecs may not use the best collections, resulting in slower iterations, GC calls, slow access, or unsupported thread-safe access. Experiments are running to see what is the best option for every case. Potential collection changes may result in API updates, please be aware of this.

- Right now you can't add more, than one component to entity in one system. There's a patch coming to have multiple component migrations, but it's not possible yet.

- There's no API to delete entity and reuse it. It's a plan to release an update with this functionality ASAP.

- The repository is not having tests yet included, because I want to check if I can use github actions for them.

---

## First performance tests:

###### Tests were captured in Unity Editor. They are not benchmarks and do not mean a thing

1. Create 500_000 entities with 4 components: 

    Allocations: 3.5 mb; Time: 91 ms
    
2. Iterate over 500_000 entities with 4 components in 4 systems: 
   
    Allocations: 8 kb; Time:  13.07 ms

3. Add 1 component to 500_000 entities with 4 components 

    Allocations: 5.9 mb; Time: 507 ms
