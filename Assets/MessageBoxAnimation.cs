using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MessageBoxAnimation : MonoBehaviour {
    public float waitSeconds = 5.0f;
    public float fadeSpeed = 4.0f;
    void Start()
    {
        StartCoroutine(Animate());
    }
    public void SetText(string text)
    {
        transform.GetComponentInChildren<Text>().text = text;
    }
    IEnumerator Animate()
    {
        var rt = GetComponent<RectTransform>();
        var pos = rt.anchoredPosition;
        var wait1 = new WaitForSeconds(0.025f);
        var wait2 = new WaitForSeconds(waitSeconds);
        while (rt.anchoredPosition.x > 0)
        {
            pos.x = rt.anchoredPosition.x - fadeSpeed;
            rt.anchoredPosition = pos;
            yield return wait1;
        }
        yield return wait2;
        while (rt.anchoredPosition.x < rt.rect.width)
        {
            pos.x = Mathf.Min(rt.anchoredPosition.x + fadeSpeed, rt.rect.width);
            rt.anchoredPosition = pos;
            yield return wait1;
        }
        Destroy(this.gameObject);
    }
}
