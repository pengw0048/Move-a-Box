using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float mouseSensitivity = 5.0f;
    public float jumpSpeed = 20.0f;
    float yRotation = 0;
    public float yRange = 60.0f;
    float zSpeed = 0;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        var cc = GetComponent<CharacterController>();
        var xRotation = Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.Rotate(0, xRotation, 0);
        yRotation -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        yRotation = Mathf.Clamp(yRotation, -yRange, yRange);
        Camera.main.transform.localRotation = Quaternion.Euler(yRotation, 0, 0);

        var xSpeed = Input.GetAxis("Horizontal") * moveSpeed;
        var ySpeed = Input.GetAxis("Vertical") * moveSpeed;
        if (cc.isGrounded)
        {
            zSpeed = 0;
            if(Input.GetButtonDown("Jump")) zSpeed = jumpSpeed;
        }else zSpeed += Physics.gravity.y * Time.deltaTime;
        var speed = new Vector3(xSpeed, zSpeed, ySpeed);
        speed = transform.rotation * speed;
        cc.Move(speed * Time.deltaTime);

        if (transform.position.y < -5)
        {
            transform.position = new Vector3(0, 20, 0);
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}
