using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class PickupObject : MonoBehaviour
{
    public float maxDistance;

    public bool carrying;
    GameObject carriedObject;
    GameObject[] backPack;
    public Text[] backPackText;
    public Canvas canvas;
    public Text backPackTextPrefeb;
    public int backPackCapacity;
    public float holdDistance;
    public float smooth;

    enum PositionSnapMode { Off, Position, Orientation, Both }
    enum TransformMode { Off, RotateHorizontally, RotateVertically, Scale, ScaleX, ScaleY, ScaleZ }
    enum TransformSnapMode { Off, On }
    PositionSnapMode positionSnapMode;
    TransformMode transformMode;
    TransformSnapMode transformSnapMode;
    public Text positionSnapModeText;
    public Text transformModeText;
    public Text transformSnapModeText;
    public float transformSensitivity;
    public float minScale;

    public GameObject player;

    void Start()
    {
        UpdateModeText();
        backPack = new GameObject[backPackCapacity];
        backPackText = new Text[backPackCapacity];
        for (int i = 0; i < backPackCapacity; i++)
        {
            backPackText[i] = Instantiate(backPackTextPrefeb) as Text;
            backPackText[i].transform.SetParent(canvas.transform);
            backPackText[i].rectTransform.anchoredPosition = new Vector2(-10f, -10f - 20f * i);
        }
        ShowBackPackItem();
    }

    void UpdateModeText()
    {
        positionSnapModeText.text = "Position snap: " + positionSnapMode;
        if (carrying)
        {
            JumpToNextLegalTransformMode();
            transformModeText.text = "Transform mode: " + transformMode;
        }
        else transformModeText.text = "";
        if (carrying && transformMode != TransformMode.Off) transformSnapModeText.text = "Transform snap: " + transformSnapMode;
        else transformSnapModeText.text = "";
    }

    void JumpToNextLegalTransformMode()
    {
        if (carriedObject.GetComponent<GroupHolder>() != null)
        {
            transformMode = TransformMode.Off;
            return;
        }
        while (true)
        {
            if (transformMode == TransformMode.Off) break;
            if (transformMode == TransformMode.RotateHorizontally && carriedObject.GetComponent<Pickupable>().canRotateHorizontally) break;
            if (transformMode == TransformMode.RotateVertically && carriedObject.GetComponent<Pickupable>().canRotateVertically) break;
            if (transformMode == TransformMode.Scale && carriedObject.GetComponent<Pickupable>().canScale) break;
            if ((transformMode == TransformMode.ScaleX || transformMode == TransformMode.ScaleY || transformMode == TransformMode.ScaleZ) && carriedObject.GetComponent<Pickupable>().canScaleXYZ) break;
            transformMode = transformMode.Next();
        }
    }

    void Update()
    {
        HandleCarry();
        if (Input.GetButtonDown("PosSnap"))
        {
            positionSnapMode = positionSnapMode.Next();
            UpdateModeText();
        }
    }

    void HandleCarry()
    {
        if (carrying)
        {
            PoseCarryingObject();
            if (Input.GetButtonDown("TransSnap") && transformMode != TransformMode.Off)
            {
                transformSnapMode = transformSnapMode.Next();
                UpdateModeText();
            }
            if (Input.GetButtonDown("Submit"))
            {
                transformMode = transformMode.Next();
                UpdateModeText();
            }
            if (transformMode != TransformMode.Off)
            {
                HandleTransform();
            }
            if (Input.GetButtonDown("Fire1"))
            {
                PutDownCarryingObject();
                UpdateModeText();
            }
            else if (Input.GetButtonDown("Fire2"))
            {
                PutCarryingIntoBackPack();
                ShowBackPackItem();
                UpdateModeText();
            }
        }
        else
        {
            if (Input.GetButtonDown("Fire1"))
            {
                PickupAnObject();
                UpdateModeText();
            }
        }
        for (int i = 0; i < backPackCapacity; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i) && backPack[i] != null)
            {
                if (carrying) SwitchCarriedWithBackPack(i);
                else TakeOutFromBackPack(i);
                UpdateModeText();
            }
    }
    bool PutCarryingIntoBackPack()
    {
        for (int i = 0; i < backPackCapacity; i++) if (backPack[i] == null)
            {
                backPack[i] = carriedObject;
                carriedObject.SetActive(false);
                carrying = false;
                carriedObject = null;
                return true;
            }
        return false;
    }
    void TakeOutFromBackPack(int i)
    {
        if (!carrying)
        {
            carrying = true;
            carriedObject = backPack[i];
            backPack[i] = null;
            carriedObject.SetActive(true);
            ShowBackPackItem();
            carriedObject.transform.position = Camera.main.transform.position + Camera.main.transform.forward * holdDistance;
            if (carriedObject.gameObject.GetComponent<GroupHolder>() != null) { positionSnapMode = PositionSnapMode.Off; UpdateModeText(); }
        }
    }
    void SwitchCarriedWithBackPack(int i)
    {
        var tempObject = backPack[i];
        tempObject.SetActive(true);
        carriedObject.SetActive(false);
        backPack[i] = carriedObject;
        carriedObject = tempObject;
        carriedObject.transform.position = Camera.main.transform.position + Camera.main.transform.forward * holdDistance;
        ShowBackPackItem();
    }
    void PickupAnObject()
    {
        var ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            var p = hit.collider.GetComponent<Pickupable>();
            if (p!=null && p.gameObject.transform.parent != null) p = p.gameObject.transform.parent.GetComponent<Pickupable>();
            var g = hit.collider.GetComponent<ResourceGenerator>();
            if (p != null) carriedObject = p.gameObject;
            if (g != null)
            {
                if (!g.TakeOne()) return;
                carriedObject = Instantiate(g.generatedObject, g.gameObject.transform.position, g.gameObject.transform.rotation) as GameObject;
                if (g.removeIfNone && g.ShouldDisappear()) Destroy(hit.collider.gameObject);
            }
            if (p != null || g != null)
            {
                carrying = true;
                if (carriedObject.GetComponent<Rigidbody>() != null) carriedObject.GetComponent<Rigidbody>().isKinematic = true;
                if (carriedObject.GetComponent<Collider>() != null) carriedObject.GetComponent<Collider>().isTrigger = true;
                if (carriedObject.gameObject.GetComponent<GroupHolder>() != null) { positionSnapMode = PositionSnapMode.Off; UpdateModeText(); }
            }
        }
    }
    void PutDownCarryingObject()
    {
        carrying = false;
        if (carriedObject.GetComponent<Rigidbody>() != null) carriedObject.GetComponent<Rigidbody>().isKinematic = false;
        if (carriedObject.GetComponent<Collider>() != null) carriedObject.GetComponent<Collider>().isTrigger = false;
        PositionSnap(carriedObject);
    }
    void PoseCarryingObject()
    {
        var pos = Vector3.Lerp(carriedObject.transform.position, Camera.main.transform.position + Camera.main.transform.forward * holdDistance, Time.deltaTime * smooth);
        if (carriedObject.GetComponent<GroupHolder>() == null)
        {
            var bound = carriedObject.GetComponent<Renderer>().bounds;
            if (bound.min.y < 0.1) pos.y += 0.1f - bound.min.y;
        }else
        {
            var miny = float.MaxValue;
            foreach (Transform obj in carriedObject.transform)
            {
                miny = Mathf.Min(miny, obj.gameObject.GetComponent<Renderer>().bounds.min.y);
                if (miny < 0.1) pos.y += 0.1f - miny;
            }
        }
        carriedObject.transform.position = pos;
        
    }
    void PositionSnap(GameObject obj)
    {
        if (positionSnapMode == PositionSnapMode.Position || positionSnapMode == PositionSnapMode.Both)
        {
            obj.transform.position = new Vector3(Mathf.Round(obj.transform.position.x), obj.transform.position.y, Mathf.Round(obj.transform.position.z));
        }
        if (positionSnapMode == PositionSnapMode.Orientation || positionSnapMode == PositionSnapMode.Both)
        {
            obj.transform.rotation = Quaternion.Euler(0, Helpers.Wrap(Helpers.NearestRound(obj.transform.rotation.eulerAngles.y, 90), 90), 0);
        }
    }
    void ShowBackPackItem()
    {
        for (int i = 0; i < backPackCapacity; i++)
        {
            var str = "Item " + (i + 1) + ": ";
            if (backPack[i] == null) str += "Empty";
            else str += backPack[i].GetComponent<Pickupable>().displayName;
            backPackText[i].text = str;
        }
    }
    void HandleTransform()
    {
        if (transformSnapMode == TransformSnapMode.On && Input.GetButtonDown("Size") || transformSnapMode == TransformSnapMode.Off && (Mathf.Abs(Input.GetAxis("Size")) > 0.01|| Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.01))
        {
            var scale = carriedObject.transform.localScale;
            var value = Mathf.Abs(Input.GetAxis("Size")) > Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) ? Input.GetAxis("Size") : Input.GetAxis("Mouse ScrollWheel");
            var factor = transformSnapMode == TransformSnapMode.On ? (value > 0 ? 1.0f : -1.0f) : value * transformSensitivity;
            if (transformMode == TransformMode.RotateHorizontally)
            {
                var rotateValue = 45.0f * factor;
                if (transformSnapMode == TransformSnapMode.On) rotateValue = Helpers.NearestRound(rotateValue, 45.0f);
                carriedObject.transform.Rotate(0.0f, rotateValue, 0.0f);
            }
            else if (transformMode == TransformMode.RotateVertically)
            {
                var rotateValue = 45.0f * factor;
                if (transformSnapMode == TransformSnapMode.On) rotateValue = Helpers.NearestRound(rotateValue, 45.0f);
                carriedObject.transform.Rotate(rotateValue, 0.0f, 0.0f);
            }
            else
            {
                if (transformSnapMode == TransformSnapMode.On)
                {
                    if (transformMode == TransformMode.ScaleX || transformMode == TransformMode.Scale) scale.x = Helpers.NearestRound(scale.x + factor, 1.0f);
                    if (transformMode == TransformMode.ScaleY || transformMode == TransformMode.Scale) scale.y = Helpers.NearestRound(scale.y + factor, 1.0f);
                    if (transformMode == TransformMode.ScaleZ || transformMode == TransformMode.Scale) scale.z = Helpers.NearestRound(scale.z + factor, 1.0f);
                }else
                {
                    if (transformMode == TransformMode.ScaleX) scale.x += factor;
                    else if (transformMode == TransformMode.ScaleY) scale.y += factor;
                    else if (transformMode == TransformMode.ScaleZ) scale.z += factor;
                    else if (transformMode == TransformMode.Scale) scale += Vector3.one * factor;
                }
                if (scale.x > minScale && scale.y > minScale && scale.z > minScale) carriedObject.transform.localScale = scale;
            }
        }
    }
}

public static class Helpers
{
    public static float NearestRound(float x, float delX)
    {
        if (delX < 1)
        {
            float i = (float)Math.Floor(x);
            float x2 = i;
            while ((x2 += delX) < x) ;
            float x1 = x2 - delX;
            return (Math.Abs(x - x1) < Math.Abs(x - x2)) ? x1 : x2;
        }
        else
        {
            return (float)Math.Round(x / delX, MidpointRounding.AwayFromZero) * delX;
        }
    }
    public static float Wrap(float x, float mod) {
        if (x > mod - 0.001) x = 0;
        return x;
    }
    public static T Next<T>(this T src) where T : struct
    {
        if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argumnent {0} is not an Enum", typeof(T).FullName));

        T[] Arr = (T[])Enum.GetValues(src.GetType());
        int j = Array.IndexOf<T>(Arr, src) + 1;
        return (Arr.Length == j) ? Arr[0] : Arr[j];
    }
}
