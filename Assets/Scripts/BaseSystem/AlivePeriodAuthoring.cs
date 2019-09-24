using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;

namespace UTJ {

public struct AlivePeriod  : IComponentData
{
	public float StartTime;
	public float Period;
    public static AlivePeriod Create(float time, float period)
    {
        return new AlivePeriod { StartTime = time, Period = period, };
    }
    public void Reset(float time)
    {
        StartTime = time;
    }
    public float GetRemainTime(float time)
    {
        return Period - (time - StartTime);
    }
}

public class AlivePeriodAuthoring : UnityEngine.MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new AlivePeriod());
    }
}

} // namespace UTJ {
