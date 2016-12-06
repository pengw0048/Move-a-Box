using UnityEngine;
using System.Collections;

public class DestroyByBoundary : MonoBehaviour {
    void OnTriggerEnter(Collider other)
    {
        if (other.tag == null || other.tag != "Player")
        {
            if (other.GetComponent<Rigidbody>() != null)
                other.GetComponent<Rigidbody>().isKinematic = true;
        }
    }
}
