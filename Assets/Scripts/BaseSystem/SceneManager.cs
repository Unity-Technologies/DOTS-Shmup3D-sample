using UnityEngine;
using Unity.Entities;

namespace UTJ {
public class SceneManager : MonoBehaviour
{
    public static int Heltz = 60;
    public static int Num = 238;
    public UnityEngine.UI.Slider NumSlider;
    public UnityEngine.UI.Text NumText;
    public UnityEngine.UI.Slider HzSlider;
    public UnityEngine.UI.Text HzText;

    void Start()
    {
        NumSlider.value = Num;
        HzSlider.value = Heltz;
    }

    public void OnPressStart()
    {
        UnityEngine.Time.fixedDeltaTime = 1f/(float)Heltz;
        UnityEngine.SceneManagement.SceneManager.LoadScene("main");
    }

    public void OnNumSliderChange(float value)
    {
        Num = (int)value;
        NumText.text = string.Format("{0}", Num);
    }
    public void OnDTSliderChange(float value)
    {
        Heltz = (int)value;
        HzText.text = string.Format("{0}Hz", Heltz);
    }

    public static void CleanUpEntities()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var entities = em.GetAllEntities();
        for (var i = 0; i < entities.Length; ++i)
        {
            em.DestroyEntity(entities[i]);
        }
    }
}

} // namespace UTJ {
