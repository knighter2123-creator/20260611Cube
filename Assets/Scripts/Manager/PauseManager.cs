using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    public GameObject pausePanel;

    public bool IsPaused => isPaused; // public으로 변경
    private bool isPaused = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    void Start()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
            PauseGame();
        else
            ResumeGame();
    }

    void PauseGame()
    {
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
        Debug.Log("게임 일시정지");
    }

    void ResumeGame()
    {
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
        Debug.Log("게임 진행");
    }
}