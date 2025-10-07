using System;
using System.Collections.Generic;

namespace Bucket.Ecs.v3
{
    public class Config
    {
        public const byte FilterBitsPerComponent = 2;
        public const byte FilterBitsPerDependency = 2;

        public const short DefaultChunkAmountInArchetype = 2;
        public const short DefaultAmountOfArchetypesInFilter = 10;
        public const short DefaultAmountOfArchetypes = 256;
        public const short DefaultFiltersAmount = 100;
        public const short DefaultChunkEntitiesCount = 4096;
        public const int DefaultChunkMemorySize = 4 * 4096;

        public static ChunkMemoryMode MemoryMode = ChunkMemoryMode.FixNumberOfEntities;
        
        public static short ChunkEntitiesCount = DefaultChunkEntitiesCount;
        public static int ChunkMemorySize = DefaultChunkMemorySize;
        public static short ExpectedChunkAmountInArchetype = DefaultChunkAmountInArchetype;
        public static short ExpectedAmountOfArchetypesInFilter = DefaultAmountOfArchetypesInFilter;
        public static short ExpectedAmountOfArchetypes = DefaultAmountOfArchetypes;
        public static short ExpectedFiltersAmount = DefaultFiltersAmount;

        public static bool RebuildSystemsDependencyGraphEachFrame = false;
    }

    public enum ChunkMemoryMode
    {
        FixNumberOfEntities,
        FitInCache
    }
    
    public class RUNTIME_REFERENCES
    {
        public static int MultiThreadSystemsCount = 100;
        
        public static short ComponentsCount = 100;
        
        public static short UnmanagedComponentsCount = 100;
    }
}