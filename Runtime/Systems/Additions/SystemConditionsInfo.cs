namespace Bucket.Ecs.v3
{
    public struct SystemConditionsInfo 
    {
        public static SystemConditionsInfo DelayByFrame(int delayFrameDelta) => new SystemConditionsInfo() { IsDelayedByFrame = true, DelayFrameDelta = (ulong)delayFrameDelta };
        public static SystemConditionsInfo DelayByTime(double delayTimeDelta) => new SystemConditionsInfo() { IsDelayedByTime = true, DelayTimeDelta = delayTimeDelta };

        public bool IsDelayedByFrame;
        public ulong LastUpdatedFrame;
        public ulong DelayFrameDelta;

        public bool IsDelayedByTime;
        public double LastUpdatedTime;
        public double DelayTimeDelta;
    }
}
