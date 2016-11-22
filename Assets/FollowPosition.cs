using UnityEngine;
using System.Collections;

public class FollowPosition : MonoBehaviour {
    public GameObject following;
	void Update () {
        if (following != null) transform.position = following.transform.position;
    }
}
