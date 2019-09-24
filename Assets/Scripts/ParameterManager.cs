using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace UTJ {
public class ParameterManager : MonoBehaviour
{
    static ParameterManager _instance;
    public static ParameterManager Instance {
        get
        {
            if (_instance == null) {
                _instance = GameObject.Find("parameter_manager").GetComponent<ParameterManager>();
            } return _instance;
        }
    }
    public static ParameterScriptableObject Parameter => Instance.mParameter;
    public ParameterScriptableObject mParameter;
}

} // namespace UTJ {
