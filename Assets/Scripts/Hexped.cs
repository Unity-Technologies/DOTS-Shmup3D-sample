using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Rendering;
using UnityEngine.Assertions;
using Random = Unity.Mathematics.Random;

namespace UTJ {

public struct HexpedBody
{
    public Entity Entity;
    public RigidTransform Transform;
    public RigidTransformMotion Motion;

    public void Init(EntityManager em, RigidTransform transform, Entity entity)
    {
        Entity = entity;
        Transform = transform;
        Motion = new RigidTransformMotion { Linear = float3.zero, Angular = float3.zero, };

        var buffer = em.GetBuffer<LegTransform>(Entity);
		buffer.Clear();
		for (var i = 0; i < HexpedConfig.Six; ++i) {
			buffer.Add(new LegTransform {
				Groin = RigidTransform.identity,
				Thigh = RigidTransform.identity,
				Shin = RigidTransform.identity,
			});
		}	
    }

    public void Update(float3 targetPos, float dt)
    {
        Motion.AddSpringForce(in Transform, targetPos, 10f /* ratio */, dt);
        Motion.AddSpringTorqueHorizontal(in Transform, 4f /* ratio */, dt);
        Motion.Update(ref Transform, dt);
    }

    public void ApplyTransform(EntityManager em)
    {
        em.SetComponentData(Entity, new Translation { Value = Transform.pos, });
        em.SetComponentData(Entity, new Rotation { Value = Transform.rot, });
    }
    public RigidTransform ReadbackTransform(EntityManager em)
    {
        var translation = em.GetComponentData<Translation>(Entity);
        var rotation = em.GetComponentData<Rotation>(Entity);
        Transform = new RigidTransform(rotation.Value, translation.Value);
        return Transform;
    }
}

public struct HexpedLeg
{
    int _id;
    Entity _entityGroin;
    Entity _entityThigh;
    Entity _entityShin;
    RigidTransform _transformGroin;
    RigidTransform _transformThigh;
    RigidTransform _transformShin;
    float _localGroinYaw;
    float _localThighPitch;
    float _localShinPitch;

    float normalize_radian(float rad)
    {
        while (rad > math.PI)
            rad -= math.PI*2f;
        while (rad < -math.PI)
            rad += math.PI*2f;
        return rad;
    }

    public void init(in HexpedData data,
					 int id,
                     EntityManager em,
					 Entity parentEntity, 
                     in RigidTransform bodyTransform,
					 in RigidTransform plane0,
					 in RigidTransform plane1,
                     Entity entityGroin,
                     Entity entityThigh,
                     Entity entityShin)
    {
        _id = id;
        _entityGroin = entityGroin;
        em.SetComponentData(_entityGroin, new HexpedGroinComponent { Id = id, Parent = parentEntity, });
        _entityThigh = entityThigh;
        em.SetComponentData(_entityThigh, new HexpedThighComponent { Id = id, Parent = parentEntity, });
        _entityShin = entityShin;
        em.SetComponentData(_entityShin, new HexpedShinComponent { Id = id, Parent = parentEntity, });
        
		var planeTransform = _id%2==0 ? plane0 : plane1;
		var planeTarget = math.transform(planeTransform, data.RegularPoint[_id]);
        SolveJoints(in data, in bodyTransform, planeTarget);
        CalcJoints(in data, in bodyTransform);
    }

    void SolveJoints(in HexpedData data,
					 in RigidTransform bodyTransform,
					 float3 planeTarget)
    {
        var bodyLocalTarget = math.transform(math.inverse(bodyTransform), planeTarget);

        var tvec = bodyLocalTarget - (data.GroinPositions[_id]);
        var yaw = math.atan2(tvec.x, tvec.z);
        var yaw0 = normalize_radian(yaw - data.GroinYaws[_id]);
        _localGroinYaw = yaw0;
        {
            var groinPosition = data.GroinPositions[_id];
            var groinRotation = quaternion.RotateY(yaw);
            var transformGroin = new RigidTransform(groinRotation, groinPosition);
            tvec = bodyLocalTarget - math.transform(transformGroin, data.FromGroinToThigh);
        }

        var a2 = math.lengthsq(tvec);
        Assert.IsTrue(a2 > 0);
        var h = tvec.y;
        var a = math.sqrt(a2);
        var b = HexpedData.LengthThigh;
        var b2 = b*b;
        var c = HexpedData.LengthShin;
        var c2 = c*c;
        const float epsilon = 1e-07f;
        var alpha = math.abs(b*c) < epsilon ? 0f : math.acos(math.clamp((b2+c2-a2)/(2f*b*c), -1f, 1f));
        var gamma = math.abs(a*b) < epsilon ? 0f : math.acos(math.clamp((a2+b2-c2)/(2f*a*b), -1f, 1f));
        var elevationAngle = math.abs(a) < epsilon ? 1f : -math.asin(math.clamp(h/a, -1f, 1f));
        
        var pitch0 = elevationAngle - gamma;
        _localThighPitch = normalize_radian(pitch0);
        var pitch1 = math.PI - alpha;
        _localShinPitch = normalize_radian(pitch1);
    }

    void CalcJoints(in HexpedData data, in RigidTransform bodyTransform)
    {
        var groinPosition = math.transform(bodyTransform, data.GroinPositions[_id]);
        var groinRotation = math.mul(math.mul(bodyTransform.rot, data.GroinRotations[_id]), 
                                      quaternion.RotateY(_localGroinYaw));
        _transformGroin = new RigidTransform(groinRotation, groinPosition);

        var thighPosition = math.transform(_transformGroin, data.FromGroinToThigh);
        var thighRotation = math.mul(groinRotation, quaternion.RotateX(_localThighPitch));
        _transformThigh = new RigidTransform(thighRotation, thighPosition);

        var shinPosition = math.transform(_transformThigh, data.FromThighToShin);
        var shinRotation = math.mul(thighRotation, quaternion.RotateX(_localShinPitch));
        _transformShin = new RigidTransform(shinRotation, shinPosition);
    }

    public void ApplyTransform(EntityManager em)
    {
        em.SetComponentData(_entityGroin, new Translation { Value = _transformGroin.pos, });
        em.SetComponentData(_entityGroin, new Rotation { Value = _transformGroin.rot, });
        em.SetComponentData(_entityThigh, new Translation { Value = _transformThigh.pos, });
        em.SetComponentData(_entityThigh, new Rotation { Value = _transformThigh.rot, });
        em.SetComponentData(_entityShin, new Translation { Value = _transformShin.pos, });
        em.SetComponentData(_entityShin, new Rotation { Value = _transformShin.rot, });
    }

    LegTransform create_transform()
    {
        return new LegTransform {
            Groin = _transformGroin,
            Thigh = _transformThigh,
            Shin = _transformShin,
        };
    }

    public LegTransform Update(CollisionWorld world,
                               in HexpedData data,
                               in RigidTransform bodyTransform,
							   in RigidTransform planeTransform,
                               out float diffY)
    {
		float3 target = math.transform(planeTransform, data.RegularPoint[_id]);
        bool hitted = Utility.RaycastGround(world, target, out var hitPos);
        diffY = 0f;
        if (hitted) {
            if (target.y < hitPos.y) {
                target = hitPos;
                diffY = hitPos.y - target.y;
            }
        }

		SolveJoints(in data, in bodyTransform, target);
		CalcJoints(in data, in bodyTransform);

        return create_transform();
    }

    RigidTransform readback_transform(EntityManager em, Entity entity)
    {
        var translation = em.GetComponentData<Translation>(entity);
        var rotation = em.GetComponentData<Rotation>(entity);
        return new RigidTransform(rotation.Value, translation.Value);
    }

    void calculate_local_values(in HexpedData data,
                                EntityManager em,
                                ref RigidTransform inversedTransformBody)
    {
        _transformGroin = readback_transform(em, _entityGroin);
        _transformThigh = readback_transform(em, _entityThigh);
        _transformShin = readback_transform(em, _entityShin);

        {
            var tvec = math.rotate(math.mul(inversedTransformBody, _transformGroin), new float3(0, 0, 1));
            var yaw = math.atan2(tvec.x, tvec.z);
            yaw -= data.GroinYaws[_id];
            _localGroinYaw = normalize_radian(yaw);
        }
        {
            var inv = math.inverse(_transformGroin);
            var tvec = math.rotate(math.mul(inv, _transformThigh), new float3(0, 0, 1));
            var pitch = math.asin(-tvec.y);
            if (tvec.z < 0f)
                pitch = math.PI-pitch;
            _localThighPitch = normalize_radian(pitch);
        }
        {
            var inv = math.inverse(_transformThigh);
            var tvec = math.rotate(math.mul(inv, _transformShin), new float3(0, 0, 1));
            var pitch = math.asin(-tvec.y);
            if (tvec.z < 0f)
                pitch = math.PI-pitch;
            _localShinPitch = normalize_radian(pitch);
        }
    }
}

public struct Hexped
{
	int _planeTurn;
	bool _switch;
	float _movePhase;
    float3 _planeOrigin;
	float3 _targetStep;
    int _actionCount;
	RigidTransform _plane0;
	RigidTransform _plane1;
    HexpedBody _body;
    HexpedLeg _leg0;
    HexpedLeg _leg1;
    HexpedLeg _leg2;
    HexpedLeg _leg3;
    HexpedLeg _leg4;
    HexpedLeg _leg5;

    bool _hit;
    int _hitGeneration;

    public void Initialize(in HexpedData data,
                           EntityManager em,
                           in RigidTransform bodyTransform,
                           Entity entityPrefabBody,
                           Entity entityPrefabGroin,
                           Entity entityPrefabThigh,
                           Entity entityPrefabShin)
    {
		_planeTurn = 0;
		_switch = true;
		_movePhase = 0;
        _planeOrigin = bodyTransform.pos;
		_targetStep = new float3(0, 0, 0);
        _actionCount = 0;
		_plane0 = bodyTransform;
		_plane1 = bodyTransform;

        var parentEntity = em.Instantiate(entityPrefabBody);
        _body.Init(em, bodyTransform, parentEntity);
        _leg0.init(in data, 0, em, parentEntity,
				   in bodyTransform,
				   in _plane0,
				   in _plane1,
                   em.Instantiate(entityPrefabGroin),
                   em.Instantiate(entityPrefabThigh),
                   em.Instantiate(entityPrefabShin));
        _leg1.init(in data, 1, em, parentEntity,
				   in bodyTransform,
				   in _plane0,
				   in _plane1,
                   em.Instantiate(entityPrefabGroin),
                   em.Instantiate(entityPrefabThigh),
                   em.Instantiate(entityPrefabShin));
        _leg2.init(in data, 2, em, parentEntity,
				   in bodyTransform,
				   in _plane0,
				   in _plane1,
                   em.Instantiate(entityPrefabGroin),
                   em.Instantiate(entityPrefabThigh),
                   em.Instantiate(entityPrefabShin));
        _leg3.init(in data, 3, em, parentEntity,
				   in bodyTransform,
				   in _plane0,
				   in _plane1,
                   em.Instantiate(entityPrefabGroin),
                   em.Instantiate(entityPrefabThigh),
                   em.Instantiate(entityPrefabShin));
        _leg4.init(in data, 4, em, parentEntity,
				   in bodyTransform,
				   in _plane0,
				   in _plane1,
                   em.Instantiate(entityPrefabGroin),
                   em.Instantiate(entityPrefabThigh),
                   em.Instantiate(entityPrefabShin));
        _leg5.init(in data, 5, em, parentEntity,
				   in bodyTransform,
				   in _plane0,
				   in _plane1,
                   em.Instantiate(entityPrefabGroin),
                   em.Instantiate(entityPrefabThigh),
                   em.Instantiate(entityPrefabShin));

        _hitGeneration = 0;
    }

    static float2 move_toe_function(float phase, out bool end)
    {
        const float phaseMax = math.PI*1.2f;
        end = false;
		if (phase > phaseMax) {
            end = true;
        }
        float x, y;
        if (phase < math.PI*0.1f) {
            x = 0f;
            y = phase - math.PI*0.1f;
        } else if (phase < math.PI*1.1f) {
            var theta = phase - math.PI*0.1f;
            x = (1 - math.cos(theta))*0.5f;
            y = math.sin(theta);
        } else {
            x = 1f;
            y = math.PI*1.1f - phase;
        }
        return new float2(x, y);
    }

	void move_plane(float dt)
	{
		const float phaseSpeed = math.PI/2; // should be calculated with velocity.
		_movePhase += phaseSpeed * dt;
        bool end;
        float2 xy = move_toe_function(_movePhase, out end);
		if (end) {
            _switch = true;
        }
        const float height = 8f;
		float3 step = new float3(xy.x * _targetStep.x, xy.y*height, xy.x * _targetStep.z);
        float3 pos = _planeOrigin + step;
        (_planeTurn != 0 ? ref _plane0.pos : ref _plane1.pos) = pos;
	}

    void adjust_plane(ref RigidTransform plane,
                      float diffYa, float diffYb, float diffYc)
    {
        var minDiffY = math.min(diffYa, math.min(diffYb, diffYc));
        if (minDiffY > 0f) {
            plane.pos.y += minDiffY;
            // plane_origin_.y += min_diff_y;
        }
    }

    public void Update(float dt,
					   CollisionWorld world,
                       in HexpedData data,
                       in CollisionInfoComponent info,
					   in ControllerUnit controllerUnit,
                       DynamicBuffer<LegTransform> legTransformBuffer)
    {
		if (_switch) {
			_switch = false;
			_planeTurn = 1 - _planeTurn;
			_movePhase = 0;
			var localTarget = data.WayPointList[_actionCount];
            ++_actionCount;
            if (_actionCount >= data.WayPointList.Length)
                _actionCount = 0;
			var towardTarget = _body.Transform.pos + localTarget;
            var plane = (_planeTurn != 0 ? _plane0 : _plane1);
            _planeOrigin = plane.pos;
            _planeOrigin.y = _body.Transform.pos.y;
			_targetStep = towardTarget - plane.pos;
		}
		move_plane(dt);

        if (info.HitGeneration != _hitGeneration)
        {
            if (info.OpponentType == OpponentType.Missile)
            {
                Random random = new Random((uint)_body.Transform.pos.GetHashCode());
                var d = random.NextFloat2Direction() * (random.NextFloat() < 0.2f ? 50f : 10f);
                _body.Motion.AddTorqueImpulseLocal(new float3(d.x*dt, 0, d.y*dt));
            }
            _hitGeneration = info.HitGeneration;
        }
        var bodyPos = (_plane0.pos + _plane1.pos)*0.5f;
        float3 hitPos;
        bool groundHitted = Utility.RaycastGround(world, _body.Transform.pos, out hitPos);
        if (groundHitted) {
            bodyPos.y = hitPos.y + HexpedData.HeightRegular;
        }
        _body.Update(bodyPos, dt);

        legTransformBuffer[0] = _leg0.Update(world, in data, in _body.Transform, in _plane0, out var diffY0);
        legTransformBuffer[1] = _leg1.Update(world, in data, in _body.Transform, in _plane1, out var diffY1);
        legTransformBuffer[2] = _leg2.Update(world, in data, in _body.Transform, in _plane0, out var diffY2);
        legTransformBuffer[3] = _leg3.Update(world, in data, in _body.Transform, in _plane1, out var diffY3);
        legTransformBuffer[4] = _leg4.Update(world, in data, in _body.Transform, in _plane0, out var diffY4);
        legTransformBuffer[5] = _leg5.Update(world, in data, in _body.Transform, in _plane1, out var diffY5);

        adjust_plane(ref _plane0, diffY0, diffY2, diffY4);
        adjust_plane(ref _plane1, diffY1, diffY3, diffY5);
    }

    public void ApplyTransform(EntityManager em)
    {
        _body.ApplyTransform(em);
        _leg0.ApplyTransform(em);
        _leg1.ApplyTransform(em);
        _leg2.ApplyTransform(em);
        _leg3.ApplyTransform(em);
        _leg4.ApplyTransform(em);
        _leg5.ApplyTransform(em);
        em.SetComponentData(_body.Entity, new HexpedComponent { Hexped = this, });
    }
    
	public RigidTransform GetBodyTransform()
	{
		return _body.Transform;
	}
}

} // namespace UTJ {
