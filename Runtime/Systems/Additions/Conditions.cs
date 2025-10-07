namespace Bucket.Ecs.v3
{
    public interface ISystemCondition { }
    public interface IEcsSystemRunIfCondition<TCondition> where TCondition : struct, ISystemCondition { }
    public interface IEcsSystemSkipIfCondition<TCondition> where TCondition : struct, ISystemCondition { }
}