using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PopupMessageManager : MonoBehaviour {
    public List<string> msgreq = new List<string>();
    public GameObject fatalMessageHolder;
    GameObject holder;
    void Start()
    {
        holder = GameObject.Find("Popup Messages");
    }
    void Update()
    {
        lock (msgreq)
        {
            foreach (var req in msgreq)
            {
                if (req.StartsWith("Fatal"))
                {
                    Time.timeScale = 0.0f;
                    Cursor.lockState = CursorLockMode.None;
                    fatalMessageHolder.SetActive(true);
                    GameObject.Find("Fatal Error Text").GetComponent<Text>().text = "A fatal error occurred and the game cannot continue:\r\n" + req;
                    Object.FindObjectOfType<CameraController>().enabled = false;
                }
                else
                {
                    var obj = Instantiate(Resources.Load(System.IO.Path.Combine("Prefabs", "PopupMessage"))) as GameObject;
                    obj.transform.SetParent(holder.transform);
                    obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(200, (holder.transform.childCount - 1) * 80);
                    obj.GetComponentInChildren<Text>().text = req;
                }
            }
            msgreq.Clear();
        }
    }
    public void Quit()
    {
        FindObjectOfType<NetworkLayer>().Stop();
        Application.Quit();
    }
}
