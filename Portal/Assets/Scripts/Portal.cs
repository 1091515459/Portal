using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

public class Portal : MonoBehaviour
{
    [Header ("Main Settings")]
    public Portal LinkedPortal;
    public MeshRenderer screen;
    public int recursionLimit = 10;
    [Header ("Advanced Settings")]
    public float nearClipOffset = 0.05f;
    public float nearClipLimit = 0.2f;
    private Camera playerCam;
    private Camera portalCam;
    private RenderTexture viewTexture;
    private Material firstRecursionMat;
    private MeshFilter screenMeshFilter;
    private List<PortalTraveller> trackedTravellers;
    private ScriptableRenderContext renderContext;

    private void Awake()
    {
        playerCam = Camera.main;
        portalCam = GetComponentInChildren<Camera>();
        portalCam.enabled = false;
        trackedTravellers = new List<PortalTraveller> ();
        screenMeshFilter = screen.GetComponent<MeshFilter> ();
        //ZX 2022-05-03 [portalCam设置为不能看到“Portal”层，在编辑器里已经将screen的层级设置为了“Protal”]
        screen.material.SetInt ("displayMask", 1);
        if(GraphicsSettings.renderPipelineAsset.GetType().Name.Equals("UniversalRenderPipelineAsset"))
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }

    private void LateUpdate()
    {
        HandleTravellers();
    }

    private void HandleTravellers()
    {
        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerTransform = traveller.transform;
            var m = LinkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix *
                    travellerTransform.localToWorldMatrix;
            
            Vector3 offset = travellerTransform.position - transform.position;
            int portalSide = System.Math.Sign(Vector3.Dot(offset, transform.forward));
            int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));
            //ZX 2022-05-04 [如果portalSide和portalSideOld不同，说明portal的方向发生了变化，执行传送]
            if (portalSide != portalSideOld)
            {
                traveller.Teleport(transform,LinkedPortal.transform,m.GetColumn(3),m.rotation);
                //ZX 2022-05-04 [不能依赖于OnTriggerEnter/Exit被调用下一帧，因为它依赖于FixedUpdate运行时间]
                LinkedPortal.OnTravellerEnterPortal(traveller);
                trackedTravellers.RemoveAt(i);
                i--;
            }
            else
            {
                traveller.graphicsClone.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                traveller.previousOffsetFromPortal = offset;
            }
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.SceneView || camera.cameraType == CameraType.Preview)
            return;
        renderContext = context;
        UniversalRenderPipeline.RenderSingleCamera(renderContext, portalCam);
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
            //ZX 2022-05-03 [将视图从门户相机呈现到视图纹理]
            portalCam.targetTexture = viewTexture;
            //ZX 2022-05-03 [在链接门户的屏幕上显示视图纹理]
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

    //ZX 2022-05-05 [在玩家摄像机被渲染之前调用]
    public void Render()
    {
        //ZX 2022-05-06 [如果玩家没有查看链接的门户，则跳过呈现该门户的视图 ]
        if(!CameraUtility.VisibleFormCamera(LinkedPortal.screen, playerCam))
        {
            return;
        }

        CreateViewTexture();
        
        var localToWorldMatrix = playerCam.transform.localToWorldMatrix;
        var renderPositions = new Vector3[recursionLimit];
        var renderRotations = new Quaternion[recursionLimit];
        
        int startIndex = 0;
        portalCam.projectionMatrix = playerCam.projectionMatrix;
        for (int i = 0; i < recursionLimit; i++)
        {
            if (i > 0)
            {
                //ZX 2022-05-06 [如果链接的门户通过此门户不可见，则不需要递归呈现]
                if(!CameraUtility.BoundsOverlap(screenMeshFilter,LinkedPortal.screenMeshFilter,portalCam))
                {
                    break;
                }
            }
            localToWorldMatrix = transform.localToWorldMatrix*LinkedPortal.transform.worldToLocalMatrix*localToWorldMatrix;
            int renderOrderIndex = recursionLimit- i - 1;
            renderPositions[renderOrderIndex] = localToWorldMatrix.GetColumn(3);
            renderRotations[renderOrderIndex] = localToWorldMatrix.rotation; 
            
            portalCam.transform.SetPositionAndRotation(renderPositions[renderOrderIndex], renderRotations[renderOrderIndex]);
            startIndex = renderOrderIndex;

        }

        //ZX 2022-05-06 [隐藏屏幕，这样摄像头就能看到传送门]
        screen.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        LinkedPortal.screen.material.SetInt("displayMask", 0);

        for (int i = startIndex; i < recursionLimit; i++)
        {
            portalCam.transform.SetPositionAndRotation(renderPositions[i], renderRotations[i]);
            SetNearClipPlane();
            HandleClippingPlane();
            //ZX 2022-05-06 [error:Recursive rendering is not supported in SRP (are you calling Camera.Render from within a render pipeline?).]
            portalCam.Render();
            //ZX 2022-05-09 [依然是不行，需要找解决方案，参考 https://gist.github.com/limdingwen/d959fd004154a8bcd1c62a30f51d10ff#file-portal-cs-L450]
            // UniversalRenderPipeline.RenderSingleCamera(renderContext, portalCam);
            
            if(i==startIndex)
            {
                LinkedPortal.screen.material.SetInt("displayMask", 1);
            }

            //ZX 2022-05-06 [取消渲染开始时隐藏的对象]
            screen.shadowCastingMode = ShadowCastingMode.On;
        }
        // if(!VisbleFromCamera(LinkedPortal.screen, playerCam))
        // {
        //     return;
        // }
        // screen.enabled = false;
        
        // CreateViewTexture();

        // //ZX 2022-05-03 [使门户相机的位置和旋转相对于这个门户，就像玩家相机相对于链接门户一样  ]
        // var m = transform.localToWorldMatrix * LinkedPortal.transform.worldToLocalMatrix * playerCam.transform.localToWorldMatrix;
        // portalCam.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);

        // screen.enabled = true;

    }
    //使用自定义投影矩阵来对齐门户相机的近剪辑平面与门户的表面
    //注意，这会影响深度缓冲区的精度，这可能会导致屏幕空间AO等效果的问题
    private void SetNearClipPlane()
    {
        Transform clipPlaneTransform = transform;
        int dot = System.Math.Sign(Vector3.Dot(clipPlaneTransform.forward,transform.position-portalCam.transform.position));
        Vector3 camSpacePosition = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlaneTransform.position);
        Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlaneTransform.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePosition, camSpaceNormal) + nearClipOffset;
        //不要使用斜剪辑平面，如果非常接近门户，因为这似乎可以造成一些视觉伪影
        if (Mathf.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCamSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            //基于新的剪切平面更新投影
            //计算矩阵与播放器的摄像头，使播放器摄像头设置(fov等)被使用
            portalCam.projectionMatrix = portalCam.CalculateObliqueMatrix(clipPlaneCamSpace);
        }
        else
        {
            portalCam.projectionMatrix = portalCam.projectionMatrix;
        }
    }
    
    private void HandleClippingPlane()
    {
        //当对旅行者进行切片时，有两个主要的图形问题  
        // 1。 门的背面有细小的网片  
        //理想的斜剪辑平面将排序，但即使0偏移，微小的银仍然可见  
        // 2。 微小的接缝之间的切片网格，和其余的模型绘制到门户屏幕上  
        //此函数试图通过在从门户呈现视图时修改切片参数来解决这些问题  
        //如果可以更优雅地修复这个问题就好了，但这是我目前能想到的最好的办法  
        const float hideDis = -1000;
        const float showDis = 1000;
        float screenThickness = LinkedPortal.ProtectScreenFormClipping(portalCam.transform.position);
        foreach (var traveller in trackedTravellers)
        {
            if (SameSideOfPortal(traveller.transform.position, portalCamPos))
            {
                //ZX 2022-05-06 [问题一]
                traveller.SetSliceOffsetDst(screenThickness,true);
            }
            else
            {
                //ZX 2022-05-06 [问题二]
                traveller.SetSliceOffsetDst(-screenThickness,true);
            }
            //ZX 2022-05-06 [确保克隆被适当地切片，以防它通过此门户可见]
            int cloneSideOfLinkPortal = -SideOfPortal(traveller.transform.position);
            bool camSameSideAsClone = LinkedPortal.SideOfPortal(portalCamPos) == cloneSideOfLinkPortal;
            if (camSameSideAsClone)
            {
                traveller.SetSliceOffsetDst(screenThickness,true);
            }
            else
            {
                traveller.SetSliceOffsetDst(-screenThickness, true);
            }
        }

        var offsetFormPortalToCam = portalCamPos - transform.position;
        foreach (var linkedTraveller in LinkedPortal.trackedTravellers)
        {
            var travellerPos = linkedTraveller.graphicsClone.transform.position;
            var clonePos = linkedTraveller.graphicsClone.transform.position;
            bool cloneOnSameSideAsCam = LinkedPortal.SideOfPortal(travellerPos)!=SideOfPortal(portalCamPos);
            if (cloneOnSameSideAsCam)
            {
                linkedTraveller.SetSliceOffsetDst(hideDis, true);
            }
            else
            {
                linkedTraveller.SetSliceOffsetDst(showDis, true);
            }
            
            bool camSameSideAsTraveller = LinkedPortal.SameSideOfPortal(portalCamPos, travellerPos);
            if (camSameSideAsTraveller)
            {
                linkedTraveller.SetSliceOffsetDst(screenThickness, false);
            }
            else
            {
                linkedTraveller.SetSliceOffsetDst(-screenThickness, false);
            }
        }
    }

    //ZX 2022-05-05 [在为当前帧呈现任何门户相机之前调用]
    public void PrePortalRender () {
        foreach (var traveller in trackedTravellers) {
            UpdateSliceParams (traveller);
        }
        ProtectScreenFormClipping(playerCam.transform.position);
    }

    //ZX 2022-05-05 [在所有门户被渲染后调用，但在玩家摄像机渲染之前]
    public void PostPortalRender()
    {
        foreach (var traveller in trackedTravellers)
        {
            UpdateSliceParams(traveller);
        }
        ProtectScreenFormClipping(playerCam.transform.position);
    }

    void UpdateSliceParams(PortalTraveller traveller, float sliceDistance = 0.5f)
    {
        //ZX 2022-05-04 [计算视图纹理的法线]
        int side = SideOfPortal(traveller.transform.position);
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 cloneSliceNormal = LinkedPortal.transform.forward * side;

        //ZX 2022-05-04 [计算视图纹理的位置]
        Vector3 slicePosition = transform.position + sliceNormal * sliceDistance;
        Vector3 cloneSlicePosition = LinkedPortal.transform.position;

        //ZX 2022-05-04 [调整切片偏移，以便当玩家站在传送门中，另一边的对象切片不显示]
        float sliceOffset = 0;
        float cloneSliceOffset = 0;
        float screenThickness = screen.transform.localScale.z;


        //ZX 2022-05-04 [如果玩家站在另一边的传送门中，切片偏移应该取反]
        bool playerSameSideAsTraceller = SameSideOfPortal(playerCam.transform.position, traveller.transform.position);
        if(!playerSameSideAsTraceller)  
        {
            sliceOffset = -screenThickness;
        }
        bool playerSameSideAsCloneAppearing = side!=LinkedPortal.SideOfPortal(playerCam.transform.position);
        if(!playerSameSideAsCloneAppearing)
        {
            cloneSliceOffset = -screenThickness;
        }

        //ZX 2022-05-04 [设置切片的位置和偏移]
        for (int i = 0; i < traveller.originalMaterials.Length; i++)
        {
            traveller.originalMaterials[i].SetVector("_SlicePosition", slicePosition);
            traveller.originalMaterials[i].SetVector("_SliceNormal", sliceNormal);
            traveller.originalMaterials[i].SetFloat("_SliceOffset", sliceOffset);
            
            traveller.cloneMaterials[i].SetVector("_SlicePosition", cloneSlicePosition);
            traveller.cloneMaterials[i].SetVector("_SliceNormal", cloneSliceNormal);
            traveller.cloneMaterials[i].SetFloat("_SliceOffset", cloneSliceOffset);
        }
    }

    //ZX 2022-05-05 [设置传送门屏幕的厚度，这样当玩家通过时，摄像机不会夹在平面附近]
    float ProtectScreenFormClipping(Vector3 viewPoint)
    {
        float halfHeight = playerCam.nearClipPlane*Mathf.Tan(playerCam.fieldOfView*0.5f*Mathf.Deg2Rad);
        float halfWidth = halfHeight * playerCam.aspect;
        float dstToNearClipPlane = new Vector3(halfWidth, halfHeight, playerCam.nearClipPlane).magnitude;
        float screenThickness = dstToNearClipPlane;

        Transform screenT = screen.transform;
        bool camFacingSaneDirAsPortal = Vector3.Dot(transform.forward,transform.position-viewPoint)>0;
        screenT.localScale = new Vector3(screenT.localScale.x, screenT.localScale.y, screenThickness);
        screenT.localPosition = Vector3.forward * screenThickness*((camFacingSaneDirAsPortal)?0.5f:-0.5f);
        return screenThickness;
    }

    private void OnTravellerEnterPortal(PortalTraveller traveller)
    {
        if(!trackedTravellers.Contains(traveller))
        {
            traveller.EnterPortalThreshold();
            traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
            trackedTravellers.Add(traveller);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if(traveller != null)
        {
            OnTravellerEnterPortal(traveller);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if(traveller != null && trackedTravellers.Contains(traveller))
        {
            traveller.ExitPortalThreshold();
            trackedTravellers.Remove(traveller);
        }
    }
    
    int SideOfPortal (Vector3 pos) {
        return System.Math.Sign (Vector3.Dot (pos - transform.position, transform.forward));
    }
    
    bool SameSideOfPortal (Vector3 posA, Vector3 posB) {
        return SideOfPortal (posA) == SideOfPortal (posB);
    }
    
    Vector3 portalCamPos {
        get {
            return portalCam.transform.position;
        }
    }
}
