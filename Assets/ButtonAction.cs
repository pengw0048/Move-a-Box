using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ButtonAction : MonoBehaviour {
    public bool isSave;
    public string fileName;
    public PauseMenu controller;
    public void Click()
    {
        var textBox = transform.GetComponentInChildren<InputField>();
        if (textBox != null) controller.DoSaveLoad(textBox.text, true);
        else controller.DoSaveLoad(fileName, isSave);
    }
}
