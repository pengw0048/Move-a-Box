﻿using UnityEngine;
using System.Collections;

public class Pickupable : MonoBehaviour
{
    public string displayName;
    public bool canRotateHorizontally;
    public bool canRotateVertically;
    public bool canScale;
    public bool canScaleXYZ;
    public int id;
    public int owner = -1;
    void Start()
    {
        if (displayName == "") displayName = gameObject.name;
    }
}