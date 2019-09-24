using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Physics;
using Unity.Physics.LowLevel;
using Unity.Physics.Systems;
using Unity.Mathematics;
 
namespace UTJ {

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class CollisionSystem : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;
	JobHandle _dependingHandle;
    
	public void AddDependingJobHandle(JobHandle handle)
	{
		_dependingHandle = JobHandle.CombineDependencies(_dependingHandle, handle);
	}

    protected override void OnCreate()
    {
        _entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override void OnDestroy()
    {
    }

    [BurstCompile]
    struct TriggerJob : ITriggerEventsJob
    {
        [ReadOnly] public NativeSlice<RigidBody> Bodies;
        [ReadOnly] public ComponentDataFromEntity<CollisionInfoSettingComponent> Settings;
        public ComponentDataFromEntity<CollisionInfoComponent> Infos;

        unsafe void hit(in CollisionInfoSettingComponent setting,
                        ref CollisionInfoComponent info,
                        in RigidBody body,
                        in RigidBody opponent,
                        Entity opponentEntity,
                        OpponentType opponentType)
        {
            ++info.HitGeneration;
            info.Opponent = opponentEntity;
            info.OpponentType = opponentType;
            if (setting.NeedPositionNormal)
            {
                if (opponent.Collider->Type == ColliderType.Terrain)
                {
                    info.Position = body.WorldFromBody.pos;
                    info.Normal = new float3(0, 1, 0);
                }
                else
                {
                    var input = new ColliderDistanceInput() {
                        Collider = opponent.Collider,
                        Transform = opponent.WorldFromBody,
                        MaxDistance = float.MaxValue,
                    };
                    body.CalculateDistance(input, out DistanceHit distanceHit);
                    info.Position = math.transform(body.WorldFromBody, distanceHit.Position);
                    info.Normal = -distanceHit.SurfaceNormal; // seems world coordinates.
                }
            }
        }

        public void Execute(TriggerEvent collisionEvent)
        {
            if (Settings.Exists(collisionEvent.Entities.EntityA))
            {
                var setting = Settings[collisionEvent.Entities.EntityA];
                var opponentType = Settings.Exists(collisionEvent.Entities.EntityB) ? Settings[collisionEvent.Entities.EntityB].Type : OpponentType.None;
                var info = Infos[collisionEvent.Entities.EntityA];
                var body = Bodies[collisionEvent.BodyIndices.BodyAIndex];
                var opponent = Bodies[collisionEvent.BodyIndices.BodyBIndex];
                hit(in setting, ref info, in body, in opponent,
                    collisionEvent.Entities.EntityB, opponentType);
                Infos[collisionEvent.Entities.EntityA] = info;
            }
            if (Settings.Exists(collisionEvent.Entities.EntityB))
            {
                var setting = Settings[collisionEvent.Entities.EntityB];
                var opponentType = Settings.Exists(collisionEvent.Entities.EntityA) ? Settings[collisionEvent.Entities.EntityA].Type : OpponentType.None;
                var info = Infos[collisionEvent.Entities.EntityB];
                var body = Bodies[collisionEvent.BodyIndices.BodyBIndex];
                var opponent = Bodies[collisionEvent.BodyIndices.BodyAIndex];
                hit(in setting, ref info, in body, in opponent,
                    collisionEvent.Entities.EntityA, opponentType);
                Infos[collisionEvent.Entities.EntityB] = info;
            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle handle)
    {
		handle = JobHandle.CombineDependencies(_dependingHandle, handle);

        var stepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();
        var buildPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
        var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var job = new TriggerJob {
            Settings = GetComponentDataFromEntity<CollisionInfoSettingComponent>(true /* readOnly */),
            Infos = GetComponentDataFromEntity<CollisionInfoComponent>(false /* readOnly */),
            Bodies = buildPhysicsWorldSystem.PhysicsWorld.Bodies,
        };
        handle = job.Schedule(stepPhysicsWorldSystem.Simulation, ref buildPhysicsWorldSystem.PhysicsWorld, handle);

        _entityCommandBufferSystem.AddJobHandleForProducer(handle);
        return handle;
    }
}

} // namespace UTJ {
