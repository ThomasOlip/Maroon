using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VR_ResetBtnScript : MonoBehaviour
{
    public bool AllowWholeReset = true;

    private bool _inWholeResetMode = false;
    private SimulationController _simController;
    
    // Start is called before the first frame update
    void Start()
    {
        var simControllerObject = GameObject.Find("SimulationController");
        if (simControllerObject)
            _simController = simControllerObject.GetComponent<SimulationController>();
        Debug.Assert(_simController != null);
        
        _simController.OnReset += OnResetHandler;
        _simController.OnStart += OnStartHandler;
    }

    protected void Update()
    {
        if (_inWholeResetMode && _simController.SimulationRunning) _inWholeResetMode = false;
    }

    private void OnStartHandler(object sender, EventArgs e)
    {
        if (!AllowWholeReset) 
            _inWholeResetMode = false;
    }

    private void OnResetHandler(object sender, EventArgs e)
    {
        if (AllowWholeReset) 
            _inWholeResetMode = true;
    }
    
    public void ButtonResetPressed()
    {
        if (!_inWholeResetMode)
            _simController.ResetSimulation();
        else
            _simController.ResetWholeSimulation();
    }
}
