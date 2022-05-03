using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Portal : MonoBehaviour
{
    public Portal LinkedPortal;
    public MeshRenderer screen;
    private Camera playerCam;
    private Camera portalCam;
    private RenderTexture viewTexture;

    private void Awake()
    {
        playerCam = Camera.main;
        portalCam = GetComponentInChildren<Camera>();
        portalCam.enabled = false;
        //ZX 2022-05-03 03:12 [portalCam设置为不能看到“Portal”层，在编辑器里已经将screen的层级设置为了“Protal”]
        screen.material.SetInt ("displayMask", 1);
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }
    
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.SceneView || camera.cameraType == CameraType.Preview)
            return;
        UniversalRenderPipeline.RenderSingleCamera(context, portalCam);
    }

    private void CreateViewTexture()
    {
        if (viewTexture == null || viewTexture.width != Screen.width || viewTexture.height != Screen.height)
        {
            if (viewTexture != null)
            {
                viewTexture.Release();
            }
            viewTexture = new RenderTexture (Screen.width, Screen.height, 16);
            //ZX 2022-05-03 03:11 [将视图从门户相机呈现到视图纹理]
            portalCam.targetTexture = viewTexture;
            //ZX 2022-05-03 03:11 [在链接门户的屏幕上显示视图纹理]
            LinkedPortal.screen.material.SetTexture("_MainTex", viewTexture);

        }
    }
    
    static bool VisbleFromCamera(Renderer renderer,Camera camera)
    {
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        var isVisible = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
        renderer.enabled = isVisible;
        return isVisible;
    }

    //Called just before player camera is rendered
    public void Render()
    {
        if(!VisbleFromCamera(LinkedPortal.screen, playerCam))
        {
            return;
        }
        
        CreateViewTexture();

        //ZX 2022-05-03 03:14 [使门户相机的位置和旋转相对于这个门户，就像玩家相机相对于链接门户一样  ]
        var m = transform.localToWorldMatrix * LinkedPortal.transform.worldToLocalMatrix * playerCam.transform.localToWorldMatrix;
        portalCam.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);

    }

    private void OnTravellerEnterPortal()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        throw new NotImplementedException();
    }
    
    private void OnTriggerExit(Collider other)
    {
        throw new NotImplementedException();
    }
}
