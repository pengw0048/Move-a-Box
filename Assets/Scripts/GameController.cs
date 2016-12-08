// Game Controller
// Handles the main logic of the game and update from network requests

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

public class GameController : MonoBehaviour {
    bool playerView;
    // components reference
    GameObject player;
    Camera playerCamera;
    Camera birdViewCamera;
    Canvas playerViewCanvas;
    NetworkLayer net;
    PickupObject pickup;
    [HideInInspector] public List<GameObject> players = new List<GameObject>();
    [HideInInspector] Dictionary<int, GameObject> objectMap = new Dictionary<int, GameObject>();
    // use this to decide where to init players
    public float mapSizeX1, mapSizeZ1, mapSizeX2, mapSizeZ2;
    // positions and rotations of non-local players
    [HideInInspector] public Dictionary<int, Vector3> position = new Dictionary<int, Vector3>();
    [HideInInspector] public Dictionary<int, Vector3> rotation = new Dictionary<int, Vector3>();
    [HideInInspector] public bool isMultiplayer;
    // requests from networklayer to move objects in the main updating thread
    public class MoveObjectRequest
    {
        public Vector3 position, rotation, scale;
        public int oid;
    }
    [HideInInspector] public List<MoveObjectRequest> movereq = new List<MoveObjectRequest>();
    // requests from networklayer to take an object from a resource generator
    public class TakeOneRequest
    {
        public int pid, gid, oid;
    }
    [HideInInspector] public List<TakeOneRequest> takereq = new List<TakeOneRequest>();
    // requests to put down / pick up items
    [HideInInspector] public List<int> putreq = new List<int>();
    [HideInInspector] public List<int> pickreq = new List<int>();
    // request to sync the world
    public class SyncRequest
    {
        public int oid;
        public Vector3 position, rotation, velocity, angularVelocity;
    }
    [HideInInspector] public List<SyncRequest> syncreq = new List<SyncRequest>();

    // set up object references and init variables
    void Start () {
        pickup = FindObjectOfType<PickupObject>();
        net = Object.FindObjectOfType<NetworkLayer>();
        player = pickup.gameObject;
        playerCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        birdViewCamera = GameObject.Find("Bird View Camera").GetComponent<Camera>();
        playerViewCanvas = GameObject.Find("Player View Canvas").GetComponent<Canvas>();
        playerView = true;
        birdViewCamera.enabled = false;
        Object.FindObjectOfType<CameraController>().enabled = false;
        var i = 0;
        foreach (var obj in FindObjectsOfType<Pickupable>()) obj.id = ++i;
	}

    void Update()
    {
        if (!isMultiplayer && Input.GetButtonDown("SwitchCamera") && !player.GetComponent<PickupObject>().carrying) SwitchCamera();
        if (isMultiplayer) MultiplayerUpdate();
    }
    
    // in single player mode, switch between player (normal) view and bird's view
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

    // requests to init player positions in the main updating thread
    [HideInInspector] public bool initPlayersInt;
    [HideInInspector] public bool initPlayersString;
    [HideInInspector] public int initInt;
    [HideInInspector] public string initString;
    [HideInInspector] public bool initDone;
    public void InitTransform(int count)
    {
        position[0] = player.transform.position;
        rotation[0] = player.transform.rotation.eulerAngles;
        for(int i = 1; i < count; i++)
        {
            position[i] = new Vector3(Random.Range(mapSizeX1, mapSizeX2), player.transform.position.y, Random.Range(mapSizeZ1, mapSizeZ2));
            rotation[i] = new Vector3(0, Random.Range(-180.0f, 180.0f), 0);
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

    // at fixed intervals broadcast the position of local player and the holding object
    IEnumerator BroadcastMyStatus()
    {
        while (true)
        {
            net.Broadcast(string.Format("Position {0} {1} {2}", net.myid, player.transform.position.Serialize(), player.transform.rotation.eulerAngles.Serialize()));
            if (pickup.carrying)
                net.Broadcast(string.Format("Object {0} {1} {2} {3} {4}", net.myid, pickup.carriedObject.GetComponent<Pickupable>().id, pickup.carriedObject.transform.position.Serialize(), pickup.carriedObject.transform.rotation.eulerAngles.Serialize(), pickup.carriedObject.transform.localScale.Serialize()));
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    // read all requests from network layer and do these updates in the main thread
    void MultiplayerUpdate()
    {
        // init players (server)
        if (initPlayersInt)
        {
            initPlayersInt = false;
            InitPlayers(initInt);
        }
        // init players (client)
        if (initPlayersString)
        {
            initPlayersString = false;
            InitPlayers(initString, initInt);
        }
        // update player positions
        lock (position)
            foreach (var id in position.Keys)
            {
                if (id != net.myid)
                {
                    players[id].transform.position = Vector3.Lerp(players[id].transform.position, position[id], 10.0f * Time.deltaTime);
                    players[id].transform.rotation = Quaternion.Euler(Vector3.Lerp(players[id].transform.rotation.eulerAngles, rotation[id], 50.0f * Time.deltaTime));
                }
            }
        // move objects
        lock (movereq)
        {
            foreach (var req in movereq)
            {
                foreach (var item in FindObjectsOfType<Pickupable>())
                {
                    if (item.id == req.oid)
                    {
                        item.transform.position = req.position;
                        item.transform.rotation = Quaternion.Euler(req.rotation);
                        item.transform.localScale = req.scale;
                    }
                }

            }
            movereq.Clear();
        }
        // take items from resource generators
        lock (takereq)
        {
            foreach (var req in takereq)
            {
                foreach (var gen in FindObjectsOfType<ResourceGenerator>())
                {
                    if (gen.id == req.gid)
                    {
                        gen.TakeOne();
                        var obj = Instantiate(gen.generatedObject, gen.gameObject.transform.position, gen.gameObject.transform.rotation) as GameObject;
                        if (obj.GetComponent<Rigidbody>() != null) obj.GetComponent<Rigidbody>().isKinematic = true;
                        if (obj.GetComponent<Collider>() != null) obj.GetComponent<Collider>().isTrigger = true;
                        obj.GetComponent<Pickupable>().id = req.oid;
                        var nid = FindObjectOfType<PickupObject>().nextid;
                        FindObjectOfType<PickupObject>().nextid = Mathf.Max(nid, req.oid + 1);
                        if (gen.removeIfNone && gen.ShouldDisappear()) Destroy(gen.gameObject);
                    }
                }

            }
            takereq.Clear();
        }
        // put down objects
        lock (putreq)
        {
            foreach (var req in putreq)
            {
                foreach (var item in FindObjectsOfType<Pickupable>())
                    if (item.id == req)
                    {
                        if (item.gameObject.GetComponent<Rigidbody>() != null) item.gameObject.GetComponent<Rigidbody>().isKinematic = false;
                        if (item.gameObject.GetComponent<Collider>() != null) item.gameObject.GetComponent<Collider>().isTrigger = false;
                        item.owner = -1;
                    }
            }
            putreq.Clear();
        }
        // pick up items
        lock (pickreq)
        {
            foreach (var req in pickreq)
            {
                foreach (var item in FindObjectsOfType<Pickupable>())
                    if (item.id == req)
                    {
                        if (item.gameObject.GetComponent<Rigidbody>() != null) item.gameObject.GetComponent<Rigidbody>().isKinematic = true;
                        if (item.gameObject.GetComponent<Collider>() != null) item.gameObject.GetComponent<Collider>().isTrigger = true;
                    }
            }
            pickreq.Clear();
        }
        // sync the world
        lock (syncreq)
        {
            try
            {
                foreach (var req in syncreq)
                {
                    GameObject obj = null;
                    if (objectMap.ContainsKey(req.oid)) obj = objectMap[req.oid];
                    else
                        foreach (var item in FindObjectsOfType<Pickupable>())
                        {
                            if (item.id == req.oid)
                            {
                                obj = item.gameObject;
                                objectMap.Add(req.oid, item.gameObject);
                                break;
                            }
                        }
                    if ((req.position - obj.gameObject.transform.position).magnitude > 0.2f)
                    {
                        obj.gameObject.transform.position = req.position;
                        obj.gameObject.transform.rotation = Quaternion.Euler(req.rotation);
                        obj.gameObject.GetComponent<Rigidbody>().velocity = req.velocity;
                        obj.gameObject.GetComponent<Rigidbody>().angularVelocity = req.angularVelocity;
                    }
                }
            }
            catch (System.Exception ex) { Debug.Log(ex); }
            syncreq.Clear();
        }
    }
}
