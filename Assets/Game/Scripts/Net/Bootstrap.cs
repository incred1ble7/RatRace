using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouseGame.Net
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] bool isBootScene;

        void Awake()
        {
            if (isBootScene)
            {
                SceneManager.LoadScene(GameConfig.MenuScene);
                return;
            }
            if (NetworkManager.Singleton == null)
            {
                SceneManager.LoadScene(GameConfig.BootScene);
                return;
            }
            string scene = SceneManager.GetActiveScene().name;
            if (scene != GameConfig.MenuScene && !NetworkManager.Singleton.IsListening)
                SceneManager.LoadScene(GameConfig.MenuScene);
        }
    }
}
