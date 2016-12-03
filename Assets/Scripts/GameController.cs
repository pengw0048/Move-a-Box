using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameController : MonoBehaviour {
    public GameObject player;
    public Camera playerCamera;
    public Camera birdViewCamera;
    public Canvas playerViewCanvas;
    public List<GameObject> players = new List<GameObject>();
    public float mapSizeX = 10.0f, mapSizeZ = 10.0f;
    bool playerView;
	void Start () {
        playerView = true;
        birdViewCamera.enabled = false;
        Object.FindObjectOfType<CameraController>().enabled = false;
        //Time.timeScale = 0.0f;
	}
	
	void Update () {
	    if (Input.GetButtonDown("SwitchCamera"))
        {
            if (!player.GetComponent<PickupObject>().carrying) SwitchCamera();
        }
	}
    void SwitchCamera()
    {
        if (playerView)
        {
            playerCamera.enabled = false;
            birdViewCamera.enabled = true;
            birdViewCamera.transform.position = new Vector3(player.transform.position.x, birdViewCamera.transform.position.y, player.transform.position.z);
            birdViewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            player.SetActive(false);
            playerViewCanvas.enabled = false;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            birdViewCamera.GetComponent<UnitSelectionComponent>().RemoveAllSelection();
            birdViewCamera.enabled = false;
            playerCamera.enabled = true;
            player.SetActive(true);
            playerViewCanvas.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
        }
        playerView = !playerView;
    }
    public void InitPlayers(int count)
    {
        var prefab = Resources.Load(System.IO.Path.Combine("Prefabs", "Player"));
        var holder = GameObject.Find("Remote Players");
        players.Add(player);
        for (int i = 1; i < count; i++)
        {
            var aplayer = Instantiate(prefab) as GameObject;
            aplayer.transform.SetParent(holder.transform);
            aplayer.transform.position = new Vector3(Random.Range(-mapSizeX, mapSizeX), aplayer.transform.position.y, Random.Range(-mapSizeZ, mapSizeZ));
            players.Add(aplayer);
        }
    }
    public void InitPlayers(string posstr, int myid)
    {
        var prefab = Resources.Load(System.IO.Path.Combine("Prefabs", "Player"));
        var holder = GameObject.Find("Remote Players");
        var poss = posstr.Split(' ');
        for(int i = 0; i < poss.Length; i++)
        {
            var aplayer = player;
            if (i != myid)
            {
                aplayer = Instantiate(prefab) as GameObject;
                aplayer.transform.SetParent(holder.transform);
            }
            aplayer.transform.position = poss[i].DeserializeVector3();
            players.Add(aplayer);
        }
    }
}
