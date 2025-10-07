using System.Collections.Generic;
using UnsafeCollections.Collections.Unsafe;
using UnsafeCollections.Collections.Unsafe.Concurrent;

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

    public sealed partial class EcsWorld
    {
        public unsafe delegate void SchedulledCommandFn(in EntityAddress entityAddress, void* data);

        private readonly List<SchedulledCommandFn> _commands = new();
        private unsafe UnsafeMPSCQueue* _schedulledCommands;

        internal unsafe UnsafeMPSCQueue* SchedulledCommandsQueue => _schedulledCommands;

        [Inline(256)]
        private unsafe void RegisterPredefinedCommands()
        {
            _schedulledCommands = UnsafeMPSCQueue.Allocate<DeferredCommandCall>(10); // TODO: capacity from config
            RegisterCommand(DestroyEntityCommand);
            RegisterCommand(MigrateAllEntitiesCommand);
            RegisterCommand(DestroyAllEntitiesCommand);
            RegisterCommand(CreateEntityCommmand);
        }

        [Inline(256)]
        public unsafe int RegisterCommand(SchedulledCommandFn command)
        {
            int id = _commands.Count;
            _commands.Add(command);
            return id;
        }

        [Inline(256)]
        public unsafe void RegisterCommand(SchedulledCommandFn command, int commandId)
        {
            if (commandId > _commands.Count)
            {
                for (int i = _commands.Count; i < commandId; i++)
                {
                    _commands.Add(null);
                }
                _commands.Add(command);
            }
            else
            {
                BAssert.True(_commands[commandId] == null, $"Command with id {commandId} is already registered");
                _commands[commandId] = command;
            }
        }

        [Inline(256)]
        internal unsafe void DispatchSchedulledCommands()
        {
            while (UnsafeMPSCQueue.IsEmpty<DeferredCommandCall>(_schedulledCommands) == false)
            {
                if (UnsafeMPSCQueue.TryDequeue<DeferredCommandCall>(_schedulledCommands, out var call) == false)
                {
                    BLogger.Error("Failure, while trying to deqeue deffered command.");
                    break;
                }
                
                var fn = GetFunctionById(call.CommandId);
                if (fn == null)
                {
                    BLogger.Error($"Command by id {call.CommandId} does not exist or not registered");
                    continue;
                }

                fn(call.EntityAddress, call.Data);
            }
        }

        [Inline(256)]
        private SchedulledCommandFn GetFunctionById(int id)
        {
            if (id >= _commands.Count) return null;

            return _commands[id];
        }

        private unsafe void DestroyEntityCommand(in EntityAddress entityAddress, void* data)
        {
            // TODO: After destroying entity - all addresses from queued commands might become invalid. you must ensure all future commands are going to work with live entities
            DestroyEntity(in entityAddress);

        }

        private unsafe void MigrateAllEntitiesCommand(in EntityAddress entityAddress, void* data)
        {
            // TODO: After destroying entity - all addresses from queued commands might become invalid. you must ensure all future commands are going to work with live entities
            
        }

        private unsafe void DestroyAllEntitiesCommand(in EntityAddress entityAddress, void* data)
        {
            // TODO: After destroying entity - all addresses from queued commands might become invalid. you must ensure all future commands are going to work with live entities

        }

        private unsafe void CreateEntityCommmand(in EntityAddress entityAddress, void* data)
        {
            CreateEntity();
            
        }
    }
}
