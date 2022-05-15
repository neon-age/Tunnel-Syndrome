using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class App : MonoBehaviour
{
    public GameObject cam;
    public GameObject ui;
    public byte maxFrameRate;
    
    void Start()
    {
        OnValidate();
    }
    void OnValidate()
    {
        if (maxFrameRate != 0)
            Application.targetFrameRate = maxFrameRate;
    }

    private void Update()
    {
        //Debug.Log(Application.isFocused);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        Debug.Log($"on app focus {hasFocus}");
        cam.SetActive(hasFocus);
        ui.SetActive(hasFocus);
    }
}
