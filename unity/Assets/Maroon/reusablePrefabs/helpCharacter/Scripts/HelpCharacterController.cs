﻿using Maroon.GlobalEntities;
using UnityEngine;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace HelpCharacter
{
    public class HelpCharacterController : MonoBehaviour
    {
        [SerializeField]
        private float _turnSpeed = 5.0f;

        private void Start()
        {
            // is displayed only once in the laboratory room
            if (!SceneManager.GetActiveScene().name.Contains("Laboratory") || !GameManager.Instance.LabLoaded)
            {
                foreach (var helpMessage in gameObject.GetComponents<HelpMessage>())
                    helpMessage.ShowMessage();
            }
        }

        private void Update()
        {
            if (Camera.main == null)
                return;

            var direction = Camera.main.transform.position - transform.position; // set direction of help character
            direction.Normalize(); //for look rotation direction vector needs to be orthogonal

            // slerp = Rotation from X to Y. X is current rotation, Y is where player direction vector is
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), _turnSpeed * Time.deltaTime);
        }
    }
}
