using UnityEngine;
using System.Collections;

public class ResourceGenerator : MonoBehaviour {
    public bool unlimited;
    public int amount;
    public bool removeIfNone;
    TextMesh remainCount;
    public int remain;
    public GameObject generatedObject;
    public int id;
    void Start()
    {
        remainCount = transform.GetComponentInChildren<TextMesh>();
        remain = amount;
        UpdateText();
    }
    public void UpdateText()
    {
        if (remainCount == null) return;
        if (unlimited) remainCount.text = "\u221E";
        else remainCount.text = remain.ToString();
    }
    public bool TakeOne()
    {
        if (unlimited) return true;
        if (remain > 0)
        {
            remain--;
            UpdateText();
            return true;
        }
        return false;
    }
    public bool ShouldDisappear()
    {
        if (unlimited || remain > 0) return false;
        return true;
    }
}
