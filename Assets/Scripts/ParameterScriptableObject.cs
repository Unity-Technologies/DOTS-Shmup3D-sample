using UnityEngine;
using UnityEngine.Serialization;

namespace UTJ {

[System.Serializable]
public struct CameraParameter
{
    public float DampingLinear;
    public float DampingAngular;
    public float LinearSpring;
    public float AngularSpring;
}

[System.Serializable]
public struct FighterParameter
{
    public float DampingLinear;
    public float DampingAngular;
    public float ForwardImpulse;
    public float YawImpulse;
    public float PitchImpulse;
    public float RollImpulse;
    public float PitchStability;
    public float RollStability;
    public float GroundAvoidance;
    public float TowardSpringTorqueRatio;
    public float BulletVelocity;
}


[CreateAssetMenu]
public class ParameterScriptableObject : ScriptableObject
{
    public CameraParameter mCameraParameter;
    public FighterParameter mFighterParameter;
    public CameraParameter CameraParmeter => mCameraParameter;
    public FighterParameter FighterParameter => mFighterParameter;
}

} // namespace UTJ {
