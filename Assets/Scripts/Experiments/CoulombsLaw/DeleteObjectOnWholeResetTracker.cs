using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeleteObjectOnWholeResetTracker : MonoBehaviour, IResetWholeObject
{
    private List<GameObject> trackingList;

    public void ResetObject()
    {
        //do nothing
    }

    public void ResetWholeObject()
    {
        foreach (var obj in trackingList)
        {
            if (obj)
            {
                Destroy(obj);
            }
        }
        
        trackingList.Clear();
    }
}
