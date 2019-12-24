using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;

namespace UTJ {

public class AlivePeriodSystem : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;

    protected override void OnCreate()
    {
        _entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    [BurstCompile]
	struct MyJob : IJobForEachWithEntity<AlivePeriod>
	{
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public float Time;

        public void Execute(Entity entity, int jobIndex, [ReadOnly] ref AlivePeriod ap)
        {
            if (ap.GetRemainTime(Time) < 0f) {
                CommandBuffer.DestroyEntity(jobIndex, entity);
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var handle = inputDeps;
		var job = new MyJob {
            CommandBuffer = commandBuffer,
            Time = UTJ.Time.GetCurrent(),
        };
		handle = job.Schedule(this, dependsOn: handle);
        _entityCommandBufferSystem.AddJobHandleForProducer(handle);

        return handle;
    }    
}

} // namespace UTJ {
