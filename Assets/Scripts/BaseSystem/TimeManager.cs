using Unity.Entities;
using System.Runtime.CompilerServices;

namespace UTJ {

public static class Time
{
    static double _time = 0f;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetDt() { return UnityEngine.Time.fixedDeltaTime; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetCurrent() { return (float)_time; }
    public static void UpdateFrame() { _time += (double)GetDt(); }
}

public class TimeSystem : ComponentSystem
{
    struct TimeIgniter : IComponentData {}
    public static void Ignite()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
		var archeType = em.CreateArchetype(typeof(TimeIgniter));
		var entity = em.CreateEntity(archeType);
#if UNITY_EDITOR
        em.SetName(entity, "TimeIgniter");
#endif
    }

	protected override void OnCreate()
    {
        GetEntityQuery(ComponentType.ReadOnly<TimeIgniter>());
    }

	protected override void OnUpdate()
	{
        UTJ.Time.UpdateFrame();
    }
}

} // namespace UTJ {
