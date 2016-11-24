using UnityEngine;
using System.Collections;

public class FacePlayer : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        if (Camera.current!=null) transform.rotation = Quaternion.LookRotation(Camera.current.transform.forward);
	}
}
