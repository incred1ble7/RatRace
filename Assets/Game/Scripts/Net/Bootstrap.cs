using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouseGame.Net
{
    /// <summary>
    /// Boot scene: holds the NetworkManager (never destroyed) and opens the main menu.
    /// In any other scene: if the game was started without Boot (Play pressed in the editor), go to Boot first.
    /// </summary>
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
            // a level / lobby opened directly while offline makes no sense either
            string scene = SceneManager.GetActiveScene().name;
            if (scene != GameConfig.MenuScene && !NetworkManager.Singleton.IsListening)
                SceneManager.LoadScene(GameConfig.MenuScene);
        }
    }
}
