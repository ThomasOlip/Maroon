﻿using Maroon.GlobalEntities;
using UnityEngine;

namespace Maroon.Physics.Electromagnetism
{
    public class Charge : PausableObject, IGenerateE
    {
        [SerializeField] public QuantityFloat strength;

        [SerializeField] private bool enableForces;

        [SerializeField] private float forceFactor = 0.5f;

        private EField _eField;

        private const float CoulombConstant = 1f / (4 * Mathf.PI * 8.8542e-12f);

        public float Strength
        {
            get => strength.Value;
            set => strength.Value = value;
        }

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        protected override void Start()
        {
            base.Start();

            _rigidBody = GetComponent<Rigidbody>();
            _eField = GameObject.FindObjectOfType<EField>();
        }


        public Vector3 getE(Vector3 position)
        {
            var chargePosition = transform.position;

            var direction = position - chargePosition;
            var distance = Vector3.Distance(chargePosition, position);

            return (strength * direction) / (4 * Mathf.PI * PhysicalConstants.e0 * Mathf.Pow(distance, 3));
        }

        public float getEFlux(Vector3 position)
        {
            throw new System.NotImplementedException();
        }

        public float getEPotential(Vector3 position)
        {
            if (Mathf.Abs(strength) < 0.0001f) return 0f;

            var distance = CoordSystemHandler.Instance.CalculateDistanceBetween(transform.position, position);
            return CoulombConstant * strength / distance;
        }

        public float getFieldStrength()
        {
            return Strength;
        }

        protected override void HandleUpdate()
        {

        }

        protected override void HandleFixedUpdate()
        {
            if (!enableForces || !_eField) return;

            var force = strength * forceFactor * _eField.get(transform.position, gameObject);
            _rigidBody.AddForce(force);
        }
    }
}