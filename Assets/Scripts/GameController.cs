using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

public class GameController : MonoBehaviour {
    public GameObject player;
    public Camera playerCamera;
    public Camera birdViewCamera;
    public Canvas playerViewCanvas;
    public List<GameObject> players = new List<GameObject>();
    public Dictionary<int, Vector3> position = new Dictionary<int, Vector3>();
    public Dictionary<int, Vector3> rotation = new Dictionary<int, Vector3>();
    public float mapSizeX = 10.0f, mapSizeZ = 10.0f;
    bool playerView;
    NetworkLayer net;
	void Start () {
        net = Object.FindObjectOfType<NetworkLayer>();
        playerView = true;
        birdViewCamera.enabled = false;
        Object.FindObjectOfType<CameraController>().enabled = false;
        //Time.timeScale = 0.0f;
	}
	
	void Update () {
        if (initPlayersInt)
        {
            initPlayersInt = false;
            InitPlayers(initInt);
        }
        if(initPlayersString)
        {
            initPlayersString = false;
            InitPlayers(initString, initInt);
        }
	    if (Input.GetButtonDown("SwitchCamera"))
        {
            if (!player.GetComponent<PickupObject>().carrying) SwitchCamera();
        }
        lock(position)
        foreach (var id in position.Keys)
        {
            if (id != net.myid)
            {
                players[id].transform.position = position[id];
                players[id].transform.rotation = Quaternion.Euler(rotation[id]);
            }
        }
        if (net.inGame) player.GetComponentInChildren<Rigidbody>().useGravity = true;
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
    public bool initPlayersInt;
    public bool initPlayersString;
    public int initInt;
    public string initString;
    public bool initDone;
    public void InitTransform(int count)
    {
        position[0] = player.transform.position;
        rotation[0] = player.transform.rotation.eulerAngles;
        for(int i = 1; i < count; i++)
        {
            position[i] = new Vector3(Random.Range(-mapSizeX, mapSizeX), player.transform.position.y, Random.Range(-mapSizeZ, mapSizeZ));
            rotation[i] = Vector3.zero;
        }
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
            aplayer.transform.position = position[i];
            players.Add(aplayer);
        }
        StartCoroutine(BroadcastMyStatus());
        initDone = true;
    }
    public void InitPlayers(string posstr, int myid)
    {
        var prefab = Resources.Load(System.IO.Path.Combine("Prefabs", "Player"));
        var holder = GameObject.Find("Remote Players");
        Debug.Log(posstr);
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
        StartCoroutine(BroadcastMyStatus());
        initDone = true;
    }
    IEnumerator BroadcastMyStatus()
    {
        while (true)
        {
            net.Broadcast(string.Format("Position {0} {1} {2}", net.myid, player.transform.position.Serialize(), player.transform.rotation.eulerAngles.Serialize()));
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}
