using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class VoltmeterEvent : UnityEvent<string> {}

public class Voltmeter : MonoBehaviour, IResetWholeObject
{
    public IField field;
    public bool ResetToOriginalPosition = false;
    public bool CanSwitchOff = false;
    public Transform SensorPosition = null;
    
    public VoltmeterEvent onVoltageChanged;
    
    private CoulombLogic _coulombLogic;
    private Quaternion _originalRotation;
    private Vector3 _originalPosition;
    private bool _isOff = false;
    
    // Start is called before the first frame update
    void Start()
    {
        if (SensorPosition == null)
            SensorPosition = transform;
        
        var simControllerObject = GameObject.Find("CoulombLogic");
        if (simControllerObject)
            _coulombLogic = simControllerObject.GetComponent<CoulombLogic>();
        Debug.Assert(_coulombLogic != null);

        onVoltageChanged.Invoke("---");

        _originalRotation = transform.rotation;
        _originalPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (_isOff) return;
        
        var currentPos = SensorPosition.position;
        if (!gameObject.activeInHierarchy) return;
        
        var potential = field.getStrength(currentPos) * Mathf.Pow(10f, -5f);
        var potentialString = potential.ToString("0.000") + " V";
//            var potentialString = potential.ToString("0.000") + " * 10^-5 V";
//            Debug.Log("OnVoltageChanged: " + potentialString);            
        onVoltageChanged.Invoke(potentialString);
    }


    public void OnResetMovingArrows(bool in3dMode)
    {
        if (!_coulombLogic)
        {
            var simControllerObject = GameObject.Find("CoulombLogic");
            if (simControllerObject)
                _coulombLogic = simControllerObject.GetComponent<CoulombLogic>();
        }
        var dragHandler = GetComponent<PC_DragHandler>();
        if (in3dMode)
        {
            gameObject.transform.parent = _coulombLogic.scene3D.transform;
            dragHandler.SetBoundaries(_coulombLogic.minBoundary3d.gameObject, _coulombLogic.maxBoundary3d.gameObject);
            dragHandler.allowedXMovement = dragHandler.allowedYMovement = dragHandler.allowedZMovement = true;
        }
        else
        {
            gameObject.transform.parent = _coulombLogic.scene2D.transform;
            dragHandler.SetBoundaries(_coulombLogic.minBoundary2d.gameObject, _coulombLogic.maxBoundary2d.gameObject);
            dragHandler.allowedXMovement = dragHandler.allowedYMovement = true;
            dragHandler.allowedZMovement = false;
        }

        var movArrows = GetComponentInChildren<PC_ArrowMovement>();
        if (!movArrows) return;
        if (in3dMode)
        {
            movArrows.UpdateMovementRestriction(false, false, false);
            movArrows.SetBoundaries(_coulombLogic.minBoundary3d.transform, _coulombLogic.maxBoundary3d.transform);
        }
        else
        {
            movArrows.UpdateMovementRestriction(false, false, true);
            movArrows.SetBoundaries(_coulombLogic.minBoundary2d.transform, _coulombLogic.maxBoundary2d.transform);
        }
        movArrows.gameObject.SetActive(true);
    }

    public void TurnOn()
    {
        if (CanSwitchOff)
        {
            _isOff = false;
        }
    }

    public void TurnOff()
    {
        if (CanSwitchOff)
        {
            _isOff = true;
            onVoltageChanged.Invoke("---");
        }
    }
    

    public void ResetObject()
    {
        
    }

    public void ResetWholeObject()
    {
        if (ResetToOriginalPosition)
        {
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;
        }
        else
            gameObject.SetActive(false);
    }

    public void HideObject()
    {
        gameObject.SetActive(false);
        if(_coulombLogic)
            gameObject.transform.parent = _coulombLogic.transform.parent;
        onVoltageChanged.Invoke("---");
    }
}
