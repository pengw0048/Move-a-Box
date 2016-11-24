using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuCanvas;
    public CameraController cameraController;
    public GameObject mainPanel;
    public GameObject filePanel;
    public GameObject buttonPrefab;
    public GameObject buttonTextPrefab;
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
                mainPanel.SetActive(true);
                filePanel.SetActive(false);
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

    public void Back()
    {
        mainPanel.SetActive(true);
        filePanel.SetActive(false);
    }

    public void Save()
    {
        PrepareFileList(true);
    }

    public void Load()
    {
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
            var savedList = SaveLoadHelper.Load(fileName);
            var objList = FindObjectsOfType<GameObject>()
                .Where(o => (o.GetComponent<Pickupable>() != null || o.GetComponent<ResourceGenerator>() != null) && o.transform.parent == null).ToList();
            objList.ForEach(o => Destroy(o));
            foreach (var item in savedList)
            {
                var obj = Instantiate(Resources.Load(item.prefab)) as GameObject;
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
