using UnityEngine;
using System.Collections;

public class StartMenu : MonoBehaviour {
	void Start () {
        this.gameObject.transform.parent.FindChild("Multiplayer Dialog").gameObject.SetActive(false);
    }
    public void Single()
    {
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.Locked;
        this.gameObject.SetActive(false);
        Object.FindObjectOfType<CameraController>().enabled = true;
    }
    public void Multiple()
    {
        this.gameObject.SetActive(false);
        this.gameObject.transform.parent.FindChild("Multiplayer Dialog").gameObject.SetActive(true);
    }
    public void Quit()
    {
        Application.Quit();
    }
}
