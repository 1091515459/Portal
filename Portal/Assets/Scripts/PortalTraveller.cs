using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalTraveller : MonoBehaviour
{
    public Vector3 offset{ get; set; }
    public Vector3 previousOffsetFromPortal { get; set; }
    
    public virtual void Teleport(Transform target,Transform toPortal,Vector3 pos,Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }
    
    public virtual void EnterPortalThreshold()
    {
        
    }
    
    public virtual void ExitPortalThreshold()
    {
        
    }
}
