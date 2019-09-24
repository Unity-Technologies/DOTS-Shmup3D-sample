using UnityEngine;

namespace UTJ {

public class SystemManager : MonoBehaviour
{
	// singleton
	static SystemManager _instance;
	public static SystemManager Instance => _instance ?? (_instance = GameObject.Find("system_manager").GetComponent<SystemManager>());

	void Initialize()
    {
        //UnityEngine.Application.targetFrameRate = 60;
        TimeSystem.Ignite();
    }

	void Awake()
	{
		SystemManager.Instance.Initialize();
	}

	void Update()
	{
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.CleanUpEntities();
            UnityEngine.SceneManagement.SceneManager.LoadScene("menu");
        }
	}
}

} // namespace UTJ {
