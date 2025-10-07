namespace Bucket.Ecs.v3
{
#if B_ENABLE_ENTITY_PIN
    public struct Metadata : IEcsComponent
    {
        public EntityPin EntityPin;
    }
#endif
}
