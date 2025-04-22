using UnityEngine;
using UnityEngine.Serialization;

public class CameraScript : MonoBehaviour
{
    [FormerlySerializedAs("MyCamera")] 
    public Camera myCamera;
    public int cameraSpeed = 10;
    
    void Start()
    {
        if (myCamera == null)
        {
            myCamera = Camera.main;
        }
    }

    void Update()
    {
        MoveCamera();
    }
    
    private void MoveCamera()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            var oldZ = myCamera.transform.localPosition.z;
            myCamera.transform.localPosition = new Vector3(0, 0, oldZ);
        }

        if (Input.GetKey(KeyCode.RightArrow))
            myCamera.transform.Translate(new Vector3(cameraSpeed * Time.deltaTime,0,0));
        if (Input.GetKey(KeyCode.LeftArrow))
            myCamera.transform.Translate(new Vector3(-cameraSpeed * Time.deltaTime,0,0));
        if (Input.GetKey(KeyCode.DownArrow))
            myCamera.transform.Translate(new Vector3(0, -cameraSpeed * Time.deltaTime,0));
        if (Input.GetKey(KeyCode.UpArrow))
            myCamera.transform.Translate(new Vector3(0, cameraSpeed * Time.deltaTime,0));
    }
}