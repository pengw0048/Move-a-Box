using UnityEngine;
using System.Collections;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuCanvas;
    public CameraController cameraController;
    bool paused;
    // Use this for initialization
    void Start()
    {
        pauseMenuCanvas.GetComponent<Canvas>().enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Pause") && Camera.main != null)
        {
            paused = !paused;
            if (paused)
            {
                Time.timeScale = 0f;
                pauseMenuCanvas.GetComponent<Canvas>().enabled = true;
                cameraController.enabled = false;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Resume();
            }
        }
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        pauseMenuCanvas.GetComponent<Canvas>().enabled = false;
        cameraController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Quit()
    {
        Application.Quit();
    }
}
