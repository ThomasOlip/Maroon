﻿using System.Collections;
using System.Collections.Generic;
using Maroon.Physics;
using UnityEngine;

public class Capacitor : PausableObject, IResetObject
{
    private enum ChargeState {IDLE, CHARGING, DISCHARGING};

    [SerializeField]
    private GameObject plate1;

    public GameObject Plate1 { get { return plate1; } set { plate1 = value; } }

    [SerializeField]
    private GameObject plate2;

    public GameObject Plate2 { get { return plate2; } set { plate2 = value; } }

    [SerializeField]
    private float seriesResistance = 5e10f;

    private const float vacuumPermittivity = 8.8542e-12f;

    private Dielectric dielectric;

    private float capacitance;

    [SerializeField]
    private float maxVoltage = 15;

    [SerializeField]
    private float powerVoltage = 15;

    private float previousPowerVoltage = 0;

    private float voltage = 0;

    private float chargeTime = 0;

    private ChargeState chargeState = ChargeState.IDLE;

    private List<Charge> chargesOnCable = new List<Charge>();

    [SerializeField]
    private ChargePoolHandler chargePoolHander;

    protected override void Start()
    {
        base.Start();

        if(plate1 == null || plate2 == null)
        {
            Debug.LogError("Capacitor requires two plates!");
            gameObject.SetActive(false);
        }

        if(chargePoolHander == null)
        {
            chargePoolHander = GameObject.FindObjectOfType<ChargePoolHandler>();
            if (chargePoolHander == null)
                Debug.LogError("Capacitor requires a charge pool handler!");
        }

        dielectric = GameObject.FindObjectOfType<Dielectric>();
	}

    protected override void Update()
    {
        capacitance = (GetOverlapPlateArea() * vacuumPermittivity * GetRelativePermittivity()) / GetPlateDistance();

        float colorValue = -(voltage / maxVoltage - 1);
        plate1.GetComponent<Renderer>().material.color = new Color(colorValue, colorValue, 1, 0.6f);
        plate2.GetComponent<Renderer>().material.color = new Color(1, colorValue, colorValue, 0.6f);

        base.Update();
    }

    private float GetOverlapPlateArea()
    {
        float area = 0;
        float overlapWidth = 0;
        float overlapHeight = 0;      

        Vector3 plate1Size = plate1.GetComponent<Renderer>().bounds.size;
        Vector3 plate2Size = plate2.GetComponent<Renderer>().bounds.size;

        Vector3 plate1WidthCorner = plate1.transform.position + new Vector3(plate1Size.x / 2, 0, 0);
        Vector3 plate1HeightCorner = plate1.transform.position + new Vector3(0, plate1Size.y / 2, 0);

        Vector3 testDirection = plate2.transform.position - plate1.transform.position;

        if (CheckIfOverlapPlate(plate1WidthCorner, testDirection, plate2))
            overlapWidth = plate1Size.x;
        else
            overlapWidth = plate2Size.x;

        if (CheckIfOverlapPlate(plate1HeightCorner, testDirection, plate2))
            overlapHeight = plate1Size.y;
        else
            overlapHeight = plate2Size.y;

        area = overlapHeight * overlapWidth;
        return area;
    }

    private bool CheckIfOverlapPlate(Vector3 cornerPoint, Vector3 testDirection, GameObject plateToCheck)
    {
        RaycastHit hit = new RaycastHit();
        if (Physics.Raycast(cornerPoint, testDirection, out hit, GetPlateDistance()))
        {
            if (hit.collider.gameObject == plateToCheck)
                return true;
        }
        return false;
    }

    public float GetPlateDistance()
    {
        return Vector3.Distance(plate1.transform.position, plate2.transform.position);
    }

    protected override void HandleUpdate()
    {

    }

    protected override void HandleFixedUpdate()
    {
        switch(chargeState)
        {
            case ChargeState.IDLE:

                chargesOnCable.Clear();

                chargeTime = 0;

                if (powerVoltage * 0.99f > voltage)
                {
                    chargeState = ChargeState.CHARGING;
                    StartCoroutine("ElectronChargeEffect");
                }
                   
                else if (powerVoltage < voltage)
                {
                    chargeState = ChargeState.DISCHARGING;
                    StartCoroutine("ElectronChargeEffect");
                }                   

                break;

            case ChargeState.CHARGING:              
                if (voltage >= powerVoltage * 0.99f)
                {
                    chargeState = ChargeState.IDLE;
                    previousPowerVoltage = voltage;
                    break;
                }
                else
                {
                    Charge();
                    chargeTime += Time.fixedDeltaTime * 0.25f;
                }
                break;

            case ChargeState.DISCHARGING:
                if (voltage * 0.99f <= powerVoltage)
                {
                    chargeState = ChargeState.IDLE;
                    previousPowerVoltage = voltage;
                }
                else
                {
                    Discharge();
                    chargeTime += Time.fixedDeltaTime * 0.25f;
                }
                break;

            default:
                break;
        }
    }

    private IEnumerator ElectronChargeEffect()
    {
        GameObject plusCable = GameObject.Find("Cable+");
        GameObject minusCable = GameObject.Find("Cable-");

        int numberOfElectrons = Mathf.Abs(Mathf.RoundToInt(powerVoltage - voltage)) * 2;

        float electronTimeInterval = 5 * seriesResistance * capacitance / numberOfElectrons;
        float electronSpeed = 0.01f;

        while (numberOfElectrons > 0 && chargeState != ChargeState.IDLE)
        {
            Charge electron = chargePoolHander.GetNewElectron();

            chargesOnCable.Add(electron);

            PathFollower pathFollower = electron.GetComponent<PathFollower>();
            pathFollower.maxSpeed = electronSpeed;

            if (voltage > powerVoltage)
            {
                electron.transform.position = plate1.transform.position;
                pathFollower.SetPath(minusCable.GetComponent<IPath>());
            }
            else
            {
                electron.transform.position = plate2.transform.position;
                pathFollower.SetPath(plusCable.GetComponent<IPath>());
            }
                
            numberOfElectrons--;

            yield return new WaitForSecondsWithPause(electronTimeInterval);
        }
    }

    private void Charge()
    {
        voltage = previousPowerVoltage + (powerVoltage - previousPowerVoltage) * (1 - Mathf.Exp(-chargeTime / (seriesResistance * capacitance)));
    }

    private void Discharge()
    {
        voltage = powerVoltage + (previousPowerVoltage - powerVoltage) * Mathf.Exp(-chargeTime / (seriesResistance * capacitance));
    }

    public float GetElectricalFieldStrength()
    {
        float area = GetOverlapPlateArea();
        float permittivity = vacuumPermittivity * dielectric.GetRelativePermittivity();

        return (capacitance * voltage) / (area * permittivity);
    }

    public float GetVoltage()
    {
        return this.voltage;
    }

    public void getVoltageByReference(MessageArgs args)
    {
        args.value = this.voltage;
    }

    public float GetCapacitance()
    {
        return this.capacitance;
    }

    public float GetChargeValue()
    {
        return capacitance * voltage;
    }

    private float GetRelativePermittivity()
    {
        float relativePermittivity = 1.0f;
        if (dielectric != null)
            relativePermittivity = dielectric.GetRelativePermittivity();

        return relativePermittivity;
    }

    public void GetCapacitanceByReference(MessageArgs args)
    {
        args.value = this.capacitance;
    }

    public void SetPowerVoltage(float voltage)
    {
        this.powerVoltage = voltage;
    }

    public void ResetObject()
    {
        voltage = 0;
        previousPowerVoltage = 0;
        chargeTime = 0;

        chargeState = ChargeState.IDLE;

        foreach(Charge charge in chargesOnCable)
        {
            if (charge != null)
                Destroy(charge.gameObject);
        }
    }
}
