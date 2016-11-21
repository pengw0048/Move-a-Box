using UnityEngine;
using System.Collections;

public class GameController : MonoBehaviour {
    public GameObject player;
    public Camera playerCamera;
    public Camera birdViewCamera;
    bool playerView;
	void Start () {
        playerView = true;
        birdViewCamera.GetComponent<BirdViewCameraController>().enabled = false;
	}
	
	void Update () {
	    if (Input.GetButtonDown("SwitchCamera"))
        {
            SwitchCamera();
        }
	}
    void SwitchCamera()
    {
        if (playerView)
        {
            playerCamera.enabled = false;
            birdViewCamera.enabled = true;
            birdViewCamera.transform.position = new Vector3(player.transform.position.x, birdViewCamera.transform.position.y, player.transform.position.z);
            player.SetActive(false);
            birdViewCamera.GetComponent<BirdViewCameraController>().enabled = true;
        }
        else
        {
            birdViewCamera.enabled = false;
            birdViewCamera.GetComponent<BirdViewCameraController>().enabled = false;
            playerCamera.enabled = true;
            player.SetActive(true);
        }
        playerView = !playerView;
    }
}
