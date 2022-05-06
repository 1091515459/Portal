using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class CameraUtility
{
    private static readonly Vector3[] cubeCornerOffsets =
    {
        new Vector3(1, 1, 1),
        new Vector3(-1, 1, 1),
        new Vector3(-1, -1, 1),
        new Vector3(-1, -1, -1),
        new Vector3(-1, 1, -1),
        new Vector3(1, -1, -1),
        new Vector3(1, 1, -1),
        new Vector3(1, -1, 1),
    };

    public static bool VisibleFormCamera(Renderer renderer, Camera camera)
    {
        //ZX 2022-05-05 [使用几何函数工具类实现裁切]
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
    }

    public static bool BoundsOverlap(MeshFilter nearObject, MeshFilter farObject, Camera camera)
    {
        var near = GetScreenSpaceBounds(nearObject, camera);
        var far = GetScreenSpaceBounds(farObject, camera);
        // //ZX 2022-05-06 [确保远的物体确实比近的物体远]
        return near.Overlaps(far);
    }
    
    public static MinMax3D GetScreenSpaceBounds(MeshFilter meshFilter, Camera camera)
    {
        MinMax3D minMax3D = new MinMax3D(float.MaxValue, float.MinValue);
        Vector3[] screenBoundsExtents = new Vector3[8];
        var localBounds = meshFilter.sharedMesh.bounds;
        bool anyPointIsInFrontOfCamera = false;
        
        for (int i = 0; i < 8; i++)
        {
            Vector3 localSpaceCenter = localBounds.center + Vector3.Scale(localBounds.extents, cubeCornerOffsets[i]);
            Vector3 worldSpaceCenter = meshFilter.transform.TransformPoint(localSpaceCenter);
            Vector3 viewportSpaceCenter = camera.WorldToViewportPoint(worldSpaceCenter);
            
            if (viewportSpaceCenter.z > 0)
            {
                anyPointIsInFrontOfCamera = true;
            }
            else
            {
                viewportSpaceCenter.x = (viewportSpaceCenter.x<=0.5f)?1:0;
                viewportSpaceCenter.y = (viewportSpaceCenter.y<=0.5f)?1:0;
            }
            minMax3D.AddPoint(viewportSpaceCenter);
        }
        
        if (!anyPointIsInFrontOfCamera)
        {
            return new MinMax3D();
        }
        
        return minMax3D;
    }
    
    public static void GetCornerPositions(this Bounds bounds, Vector3[] corners)
    {
        Vector3 center = bounds.center;
        float extentsX = bounds.extents.x;
        float extentsY = bounds.extents.y;
        float extentsZ = bounds.extents.z;

        // bottom
        corners[0] = center + new Vector3(-extentsX, -extentsY, -extentsZ);
        corners[1] = center + new Vector3(-extentsX, -extentsY, extentsZ);
        corners[2] = center + new Vector3(extentsX, -extentsY, extentsZ);
        corners[3] = center + new Vector3(extentsX, -extentsY, -extentsZ);

        // top
        corners[4] = center + new Vector3(-extentsX, extentsY, -extentsZ);
        corners[5] = center + new Vector3(-extentsX, extentsY, extentsZ);
        corners[6] = center + new Vector3(extentsX, extentsY, extentsZ);
        corners[7] = center + new Vector3(extentsX, extentsY, -extentsZ);
    }
    
    public struct MinMax3D
    {
        public Vector3 min;
        public Vector3 max;

        public MinMax3D(float min, float max)
        {
            this.min.x = min;
            this.min.y = min;
            this.min.z = min;
            this.max.x = max;
            this.max.y = max;
            this.max.z = max;
        }
        
        public void AddPoint (Vector3 point) {
            min.x = Mathf.Min (min.x, point.x);
            min.y = Mathf.Min (min.y, point.y); 
            min.z = Mathf.Min (min.z, point.z);
            max.x = Mathf.Max (max.x, point.x);
            max.y = Mathf.Max (max.y, point.y);
            max.z = Mathf.Max (max.z, point.z);
        }
        //ZX 2022-05-06 [没有在轴上重叠]
        public bool Overlaps(MinMax3D other)
        {
            return !(min.x > other.max.x || max.x < other.min.x || min.y > other.max.y || max.y < other.min.y || min.z > other.max.z || max.z < other.min.z);
        }
        
        public bool Overlaps(Bounds other)
        {
            return !(min.x > other.max.x || max.x < other.min.x || min.y > other.max.y || max.y < other.min.y || min.z > other.max.z || max.z < other.min.z);
        }
        
        public bool Overlaps(MinMax3D other, float margin)
        {
            return !(min.x > other.max.x + margin || max.x < other.min.x - margin || min.y > other.max.y + margin || max.y < other.min.y - margin || min.z > other.max.z + margin || max.z < other.min.z - margin);
        }
        
        public bool Overlaps(Bounds other, float margin)
        {
            return !(min.x > other.max.x + margin || max.x < other.min.x - margin || min.y > other.max.y + margin || max.y < other.min.y - margin || min.z > other.max.z + margin || max.z < other.min.z - margin);
        }
        
        public bool Contains(Vector3 point)
        {
            return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y && point.z >= min.z && point.z <= max.z;
        }
        
        public bool Contains(Vector3 point, float margin)
        {
            return point.x >= min.x - margin && point.x <= max.x + margin && point.y >= min.y - margin && point.y <= max.y + margin && point.z >= min.z - margin && point.z <= max.z + margin;
        }
        
        public bool Contains(MinMax3D other)
        {
            return other.min.x >= min.x && other.max.x <= max.x && other.min.y >= min.y && other.max.y <= max.y && other.min.z >= min.z && other.max.z <= max.z;
        }
        
        public bool Contains(MinMax3D other, float margin)
        {
            return other.min.x >= min.x - margin && other.max.x <= max.x + margin && other.min.y >= min.y - margin && other.max.y <= max.y + margin && other.min.z >= min.z - margin && other.max.z <= max.z + margin;
        }
        
        public bool Contains(Bounds other)
        {
            return other.min.x >= min.x && other.max.x <= max.x && other.min.y >= min.y && other.max.y <= max.y && other.min.z >= min.z && other.max.z <= max.z;
        }
    }
}
