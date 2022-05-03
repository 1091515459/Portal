using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MainCamera : MonoBehaviour
{
    Portal[] portals;

    private void Awake()
    {
        portals = FindObjectsOfType<Portal>();
        RenderPipelineManager.beginCameraRendering += OnPreCullCustom;
    }

    //ZX 2022-05-03 01:44 [URP不支持相机OnPreCull]
    // private void OnPreCull()
    // {
    //     for (int i = 0; i < portals.Length; i++)
    //     {
    //         portals[i].Render();
    //     }
    // }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnPreCullCustom;
    }

    void OnPreCullCustom(ScriptableRenderContext context, Camera camera)
    {
        for (int i = 0; i < portals.Length; i++)
        {
            portals[i].Render();
        }
    }
}
