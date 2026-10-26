using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GamePauseController pauseController;

    private void Awake()
    {
        if (pauseController == null) pauseController = GetComponent<GamePauseController>();
        if (pauseController == null || !pauseController.IsPaused) Time.timeScale = 1f;

        if (playerHealth == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
            }
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.Died += ShowGameOver;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= ShowGameOver;
        }
    }

    private void ShowGameOver()
    {
        if (pauseController != null) pauseController.SetPaused(GamePauseReason.GameOver, true);
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            Button restart = gameOverPanel.GetComponentInChildren<Button>();
            if (restart != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(restart.gameObject);
        }

        if (pauseController == null) Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void RestartGame()
    {
        if (pauseController != null) pauseController.ResetForRestart();
        Time.timeScale = 1f;

        Scene currentScene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // WorldPrototype is also playable directly, before it is in Build Settings.
        if (currentScene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                currentScene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}
