using UnityEngine;
using VRTK;

public class VR_SnapDropZoneInitializing : MonoBehaviour
{
    public VRTK_SnapDropZone snapDropZone;
    public GameObject initObject;
    
    
    // Start is called before the first frame update
    void Start()
    {
//        SnapDropZoneEventArgs sdzArgs;
//        sdzArgs.snappedObject = initObject;

        snapDropZone.ForceSnap(initObject);
    }
}
