using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class UnitSelectionComponent : MonoBehaviour
{
    public GameObject groupHolderPrefab;
    bool isSelecting = false;
    Vector3 mousePosition1;

    public GameObject selectionCirclePrefab;

    public void RemoveAllSelection()
    {
        foreach (var selectableObject in FindObjectsOfType<SelectableUnitComponent>())
        {
            if (selectableObject.selectionCircle != null)
            {
                Destroy(selectableObject.selectionCircle.gameObject);
                selectableObject.selectionCircle = null;
            }
        }
    }

    void Update()
    {
        // If we press the left mouse button, begin selection and remember the location of the mouse
        if (Input.GetButtonDown("Fire1"))
        {
            isSelecting = true;
            mousePosition1 = Input.mousePosition;
            RemoveAllSelection();
        }
        // If we let go of the left mouse button, end selection
        if (Input.GetButtonUp("Fire1"))
        {
            var selectedObjects = new List<SelectableUnitComponent>();
            foreach (var selectableObject in FindObjectsOfType<SelectableUnitComponent>())
            {
                if (IsWithinSelectionBounds(selectableObject.gameObject) && selectableObject.gameObject.transform.parent == null)
                {
                    selectedObjects.Add(selectableObject);
                }
            }

            isSelecting = false;
        }

        // Highlight all objects within the selection box
        if (isSelecting)
        {
            foreach (var selectableObject in FindObjectsOfType<SelectableUnitComponent>())
            {
                if (IsWithinSelectionBounds(selectableObject.gameObject) && selectableObject.gameObject.transform.parent == null)
                {
                    if (selectableObject.selectionCircle == null)
                    {
                        selectableObject.selectionCircle = Instantiate(selectionCirclePrefab);
                        selectableObject.selectionCircle.transform.eulerAngles = new Vector3(90, 0, 0);
                        selectableObject.selectionCircle.GetComponent<FollowPosition>().following = selectableObject.gameObject;
                        selectableObject.selectionCircle.GetComponent<FollowPosition>().Set();
                        if (selectableObject.gameObject.GetComponent<GroupHolder>() == null)
                        {
                            var extends = selectableObject.GetComponent<Renderer>().bounds.extents;
                            extends.y = 0f;
                            selectableObject.selectionCircle.GetComponent<Projector>().orthographicSize = extends.magnitude *1.2f;
                        }
                        else
                        {
                            var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
                            var max = -min;
                            foreach (Transform obj in selectableObject.transform)
                            {
                                if (obj.gameObject.GetComponent<Renderer>() == null) continue;
                                var bound = obj.gameObject.GetComponent<Renderer>().bounds;
                                min.x = Mathf.Min(bound.min.x, min.x);
                                min.z = Mathf.Min(bound.min.z, min.z);
                                max.x = Mathf.Max(bound.max.x, max.x);
                                max.z = Mathf.Max(bound.max.z, max.z);
                            }
                            selectableObject.selectionCircle.GetComponent<Projector>().orthographicSize = (max - min).magnitude * 0.6f;
                        }
                    }
                }
                else
                {
                    if (selectableObject.selectionCircle != null)
                    {
                        Destroy(selectableObject.selectionCircle.gameObject);
                        selectableObject.selectionCircle = null;
                    }
                }
            }
        }

        if (Input.GetButtonDown("Submit"))
        {
            var selectedObjects = new List<SelectableUnitComponent>();
            foreach (var selectableObject in FindObjectsOfType<SelectableUnitComponent>())
            {
                if (selectableObject.selectionCircle != null)
                {
                    selectedObjects.Add(selectableObject);
                    Destroy(selectableObject.selectionCircle.gameObject);
                    selectableObject.selectionCircle = null;
                }
            }
            if (selectedObjects.Count < 2) return;
            var rawObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                if (obj.gameObject.GetComponent<GroupHolder>() != null)
                {
                    var children = new List<GameObject>();
                    foreach (Transform item in obj.gameObject.GetComponent<GroupHolder>().transform)
                    {
                        children.Add(item.gameObject);
                    }
                    foreach(var item in children) {
                        item.gameObject.transform.SetParent(null, true);
                        if (item.gameObject.GetComponent<FixedJoint>() != null) Destroy(item.gameObject.GetComponent<FixedJoint>());
                        rawObjects.Add(item.gameObject);
                    }
                    Destroy(obj.gameObject);
                }
                else rawObjects.Add(obj.gameObject);
            }
            var groupHolder = Instantiate(groupHolderPrefab);
            var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            var max = -min;
            foreach (GameObject obj in rawObjects)
            {
                if (obj.GetComponent<Renderer>() == null) continue;
                var bound = obj.GetComponent<Renderer>().bounds;
                min.x = Mathf.Min(bound.min.x, min.x);
                min.z = Mathf.Min(bound.min.z, min.z);
                max.x = Mathf.Max(bound.max.x, max.x);
                max.z = Mathf.Max(bound.max.z, max.z);
            }
            groupHolder.transform.position = (max + min) / 2;
            foreach (GameObject obj in rawObjects)
            {
                var rigidBody = obj.AddComponent<FixedJoint>();
                rigidBody.connectedBody = groupHolder.GetComponent<Rigidbody>();
            }
            foreach (var obj in rawObjects)
            {
                obj.transform.SetParent(groupHolder.transform);
            }
            groupHolder.GetComponent<Pickupable>().displayName = "Group of " + rawObjects.Count;
        }
        if (Input.GetButtonDown("Cancel"))
        {
            foreach (var obj in FindObjectsOfType<SelectableUnitComponent>())
            {
                if (obj.selectionCircle != null && obj.gameObject.GetComponent<GroupHolder>() != null)
                {
                    Destroy(obj.selectionCircle);
                    var children = new List<GameObject>();
                    foreach (Transform item in obj.gameObject.GetComponent<GroupHolder>().transform)
                    {
                        children.Add(item.gameObject);
                    }
                    foreach (var item in children)
                    {
                        item.gameObject.transform.SetParent(null, true);
                        if (item.gameObject.GetComponent<FixedJoint>() != null) Destroy(item.gameObject.GetComponent<FixedJoint>());
                    }
                    Destroy(obj.gameObject);
                }
            }
        }

    }

    public bool IsWithinSelectionBounds(GameObject gameObject)
    {
        if (!isSelecting)
            return false;

        var camera = GameObject.FindGameObjectWithTag("Bird View Camera").GetComponent<Camera>();
        var viewportBounds = Utils.GetViewportBounds(camera, mousePosition1, Input.mousePosition);
        return viewportBounds.Contains(camera.WorldToViewportPoint(gameObject.transform.position));
    }

    void OnGUI()
    {
        if (isSelecting)
        {
            // Create a rect from both mouse positions
            var rect = Utils.GetScreenRect(mousePosition1, Input.mousePosition);
            Utils.DrawScreenRect(rect, new Color(0.8f, 0.8f, 0.95f, 0.25f));
            Utils.DrawScreenRectBorder(rect, 2, new Color(0.8f, 0.8f, 0.95f));
        }
    }
}