namespace Bucket.Ecs.v3
{
#if B_ENABLE_ENTITY_PIN
    public class EntityPin
    {
        // TODO: consider adding a pointer to unmanaged data and reference to dynamic components directly
        public EntityAddress Address;
    }
#endif
}
