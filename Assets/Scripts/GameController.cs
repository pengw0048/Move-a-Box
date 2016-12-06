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
    public float mapSizeX1, mapSizeZ1, mapSizeX2, mapSizeZ2;
    bool playerView;
    public bool isMultiplayer;
    NetworkLayer net;
    PickupObject pickup;
    public class MoveObjectRequest
    {
        public Vector3 position, rotation, scale;
        public int oid;
    }
    public List<MoveObjectRequest> movereq = new List<MoveObjectRequest>();
    public class TakeOneRequest
    {
        public int pid, gid, oid;
    }
    public List<TakeOneRequest> takereq = new List<TakeOneRequest>();
    public List<int> putreq = new List<int>();
    public List<int> pickreq = new List<int>();
    public class SyncRequest
    {
        public int oid;
        public Vector3 position, rotation, velocity, angularVelocity;
    }
    public List<SyncRequest> syncreq = new List<SyncRequest>();
    Dictionary<int, GameObject> objectMap = new Dictionary<int, GameObject>();

    void Start () {
        pickup = FindObjectOfType<PickupObject>();
        net = Object.FindObjectOfType<NetworkLayer>();
        playerView = true;
        birdViewCamera.enabled = false;
        Object.FindObjectOfType<CameraController>().enabled = false;
        //Time.timeScale = 0.0f;
        var i = 0;
        foreach (var obj in FindObjectsOfType<Pickupable>())
        {
            obj.id = ++i;
        }
	}

    void Update()
    {
        if (!isMultiplayer && Input.GetButtonDown("SwitchCamera"))
        {
            if (!player.GetComponent<PickupObject>().carrying) SwitchCamera();
        }
        if (isMultiplayer)
        {
            if (initPlayersInt)
            {
                initPlayersInt = false;
                InitPlayers(initInt);
            }
            if (initPlayersString)
            {
                initPlayersString = false;
                InitPlayers(initString, initInt);
            }
            lock (position)
                foreach (var id in position.Keys)
                {
                    if (id != net.myid)
                    {
                        players[id].transform.position = Vector3.Lerp(players[id].transform.position, position[id], 10.0f * Time.deltaTime);
                        players[id].transform.rotation = Quaternion.Euler(Vector3.Lerp(players[id].transform.rotation.eulerAngles, rotation[id], 50.0f * Time.deltaTime));
                    }
                }
            //if (net.inGame) player.GetComponentInChildren<Rigidbody>().useGravity = true;
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
}
