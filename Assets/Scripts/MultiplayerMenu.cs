using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Threading;
using System;

public class MultiplayerMenu : MonoBehaviour {
    string defaultLabel, defaultStart;
    NetworkLayer net;
    Button hostButton, connectButton, startButton, backButton;
    InputField addressInput;
    Text label, connectionList;
    public bool triggerStart;
    void Start()
    {
        net = UnityEngine.Object.FindObjectOfType<NetworkLayer>();
        hostButton = gameObject.transform.FindChild("Host Button").GetComponent<Button>();
        connectButton = gameObject.transform.FindChild("Connect Button").GetComponent<Button>();
        startButton = gameObject.transform.FindChild("Start Button").GetComponent<Button>();
        backButton = gameObject.transform.FindChild("Back Button").GetComponent<Button>();
        addressInput = gameObject.transform.FindChild("Address Input").GetComponent<InputField>();
        label = gameObject.transform.FindChild("Info Label").GetComponent<Text>();
        connectionList = gameObject.transform.FindChild("Connection List").GetComponent<Text>();
        defaultLabel = label.text;
        defaultStart = startButton.gameObject.transform.GetComponentInChildren<Text>().text;
        connectionList.text = "Connected hosts:";
    }
    public void Back()
    {
        label.text = defaultLabel;
        startButton.gameObject.transform.GetComponentInChildren<Text>().text = defaultStart;
        this.gameObject.transform.parent.FindChild("Start Up Dialog").gameObject.SetActive(true);
        this.gameObject.SetActive(false);
    }
    public void Host()
    {
        hostButton.interactable = false;
        connectButton.interactable = false;
        addressInput.interactable = false;
        startButton.interactable = true;
        backButton.interactable = false;
        label.text = "My address: " + net.GetIPString();
        net.Host();
        StartCoroutine(RefreshList());
    }
    public void Connect()
    {
        hostButton.interactable = false;
        connectButton.interactable = false;
        addressInput.interactable = false;
        backButton.interactable = false;
        startButton.gameObject.transform.GetComponentInChildren<Text>().text = "Wait for server to start...";
        var ret = net.Connect(addressInput.text, this);
        if (ret != null) fail(ret);
        else StartCoroutine(RefreshList());
    }
    public void StartGame()
    {
        net.StartGame();
        this.gameObject.SetActive(false);
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Object.FindObjectOfType<CameraController>().enabled = true;
    }
    IEnumerator RefreshList()
    {
        while (!net.inGame)
        {
            connectionList.text = "Connected hosts:\r\n" + net.GetClientList();
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }
    public void fail(Exception ex)
    {
        label.text = ex.Message;
        hostButton.interactable = true;
        connectButton.interactable = true;
        addressInput.interactable = true;
        backButton.interactable = true;
        startButton.gameObject.transform.GetComponentInChildren<Text>().text = defaultStart;
    }
    void Update()
    {
        if (triggerStart)
        {
            triggerStart = false;
            ClientStart();
        }
    }
    void ClientStart()
    {
        this.gameObject.SetActive(false);
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Object.FindObjectOfType<CameraController>().enabled = true;
    }
}
