using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuCanvas;
    public CameraController cameraController;
    public PickupObject pickupScript;
    public GameObject mainPanel;
    public GameObject filePanel;
    public GameObject buttonPrefab;
    public GameObject buttonTextPrefab;
    bool paused;
    GameController controller;
    // Use this for initialization
    void Start()
    {
        controller = FindObjectOfType<GameController>();
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
                pickupScript.enabled = false;
                Time.timeScale = 0f;
                pauseMenuCanvas.GetComponent<Canvas>().enabled = true;
                mainPanel.SetActive(true);
                filePanel.SetActive(false);
                cameraController.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                foreach (Transform child in mainPanel.transform)
                {
                    if (child.gameObject.name.Contains("Save") || child.gameObject.name.Contains("Load")) child.GetComponent<Button>().interactable = !pickupScript.carrying;
                }
            }
            else
            {
                Resume();
            }
        }
    }

    public void Resume()
    {
        pickupScript.enabled = true;
        Time.timeScale = 1f;
        pauseMenuCanvas.GetComponent<Canvas>().enabled = false;
        cameraController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Quit()
    {
        Object.FindObjectOfType<NetworkLayer>().Stop();
        Application.Quit();
    }

    public void Back()
    {
        mainPanel.SetActive(true);
        filePanel.SetActive(false);
    }

    public void Save()
    {
        if (!controller.isMultiplayer)
            PrepareFileList(true);
    }

    public void Load()
    {
        if (!controller.isMultiplayer)
            PrepareFileList(false);
    }

    public void DoSaveLoad(string fileName, bool isSave)
    {
        if (isSave)
        {
            SaveLoadHelper.Save(fileName);
        }
        else
        {
            var objList = FindObjectsOfType<GameObject>()
                .Where(o => (o.GetComponent<Pickupable>() != null || o.GetComponent<ResourceGenerator>() != null) && o.transform.parent == null).ToList();
            objList.ForEach(o => Destroy(o));
            var savedList = SaveLoadHelper.Load(fileName);
            var objDict = new Dictionary<int, GameObject>();
            foreach (var item in savedList)
            {
                var obj = Instantiate(Resources.Load(System.IO.Path.Combine("Prefabs", item.prefab))) as GameObject;
                objDict.Add(item.index, obj);
                foreach (var component in item.components)
                {
                    if (component == "Transform")
                    {
                        obj.transform.position = item.values["Position"].DeserializeVector3();
                        obj.transform.rotation = Quaternion.Euler(item.values["Rotation"].DeserializeVector3());
                        obj.transform.localScale = item.values["Scale"].DeserializeVector3();
                    }
                    else if (component == "ResourceGenerator")
                    {
                        var gen = obj.GetComponent<ResourceGenerator>();
                        gen.remain = int.Parse(item.values["Remain"]);
                    }
                    else if (component == "GroupHolder")
                    {
                        var tokens = item.values["Children"].Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (var index in tokens)
                        {
                            var child = objDict[int.Parse(index)];
                            var rigidBody = child.AddComponent<FixedJoint>();
                            rigidBody.connectedBody = obj.GetComponent<Rigidbody>();
                            child.transform.SetParent(obj.transform);
                        }
                        obj.GetComponent<Pickupable>().displayName = "Group of " + obj.transform.childCount;
                    }
                }
            }
        }
        Resume();
    }

    void PrepareFileList(bool isSave)
    {
        mainPanel.SetActive(false);
        filePanel.SetActive(true);
        var children = new List<GameObject>();
        foreach (Transform child in filePanel.transform.GetChild(0).GetChild(0))
        {
            children.Add(child.gameObject);
        }
        foreach (var child in children)
        {
            Destroy(child);
        }
        foreach (var file in SaveLoadHelper.GetSaveFileList())
        {
            var button = Instantiate(buttonPrefab, filePanel.transform.GetChild(0).GetChild(0)) as GameObject;
            button.transform.GetChild(0).gameObject.GetComponent<Text>().text = file;
            button.GetComponent<ButtonAction>().fileName = file;
            button.GetComponent<ButtonAction>().isSave = isSave;
            button.GetComponent<ButtonAction>().controller = this;
        }
        if (isSave)
        {
            var button = Instantiate(buttonTextPrefab, filePanel.transform.GetChild(0).GetChild(0)) as GameObject;
            button.GetComponent<ButtonAction>().controller = this;
        }
    }
}
