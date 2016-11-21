using UnityEngine;
using System.Collections;

public class BirdViewCameraController : MonoBehaviour {
    public float moveSpeed = 5.0f;
	void Start () {
	
	}
	
	void Update () {
        if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01 || Mathf.Abs(Input.GetAxis("Vertical")) > 0.01)
        {
            var xSpeed = Input.GetAxis("Horizontal") * moveSpeed;
            var ySpeed = Input.GetAxis("Vertical") * moveSpeed;
            var speed = new Vector3(xSpeed, 0, ySpeed);
            transform.position += speed * Time.deltaTime;
        }
        if (Mathf.Abs(Input.GetAxis("Size")) > 0.01 && GetComponent<Camera>().orthographicSize - Input.GetAxis("Size") > 1)
        {
            GetComponent<Camera>().orthographicSize -= Input.GetAxis("Size");
        }
    }
}
