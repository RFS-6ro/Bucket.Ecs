namespace Bucket.Ecs.v3
{
    public interface ISpreadFramesSystem
    {
        int DelayFrames { get; }
    }
    
    public interface ISpreadTimestampSystem
    {
        double DelayTime { get; }
    }
}