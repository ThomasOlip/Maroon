using System.Collections.Generic;
using Maroon.Physics.Optics.Manager;
using Maroon.Physics.Optics.TableObject.OpticalComponent;
using Maroon.Physics.Optics.Util;
using UnityEngine;

namespace Maroon.Physics.Optics.Light
{
    public class LightPath
    {
        private float _wavelength;
        private List<RaySegment> _raySegments = new List<RaySegment>();

        public List<RaySegment> RaySegments
        {
            get => _raySegments;
            set => _raySegments = value;
        }
        public float Wavelength
        {
            get => _wavelength;
            set => _wavelength = value;
        }

        public LightPath(float wavelength)
        {
            this._wavelength = wavelength;
        }

        public void ResetLightRoute()
        {
            foreach (var rs in _raySegments)
            {
                rs.DestroyRaySegment();
            }

            _raySegments.Clear();
        }
        
        public void CalculateNextRay(RaySegment inRay)
        {
            // Stop when maximal number of rays (per light route) is reached
            if (_raySegments.Count >= Constants.MaxNumberOfRays)
                return;
            
            // Get the first hit component
            OpticalComponent hitComponent = OpticalComponentManager.Instance.GetFirstHitComponent(inRay.r0Local, inRay.n);
            
            switch (hitComponent)
            {
                // End of ray - no further reflection/refraction
                case Wall wall:
                    (float distanceToWall, _, _) = wall.CalculateDistanceReflectionRefraction(inRay);
                    
                    inRay.UpdateLength(distanceToWall);
                    break;
                
                case Aperture aperture:
                    (float distanceToAperture, _, _) = aperture.CalculateDistanceReflectionRefraction(inRay);
                    
                    inRay.UpdateLength(distanceToAperture);
                    break;
                        
                // Ray has 1 further reflection
                case TableObject.OpticalComponent.Mirror mirror:
                    (float distanceToMirror, RaySegment reflectionM, _) = mirror.CalculateDistanceReflectionRefraction(inRay);
                    
                    inRay.UpdateLength(distanceToMirror);
                    AddRaySegment(reflectionM);
                    CalculateNextRay(reflectionM);
                    break;

                // Ray has potential reflection and refraction
                case Eye eye:
                    (float distanceToEye, RaySegment reflectionEye, RaySegment refractionEye) = eye.CalculateDistanceReflectionRefraction(inRay);

                    inRay.UpdateLength(distanceToEye);
                    if (reflectionEye != null)
                    {
                        AddRaySegment(reflectionEye);
                        CalculateNextRay(reflectionEye);
                    }

                    if (refractionEye != null)
                    {
                        AddRaySegment(refractionEye);
                        CalculateNextRay(refractionEye);
                    }
                    break;
                
                // Ray has reflection and refraction
                case Lens lens:
                    (float distanceToLens, RaySegment reflectionLens, RaySegment refractionLens) = lens.CalculateDistanceReflectionRefraction(inRay);
                    inRay.UpdateLength(distanceToLens);
                    if (reflectionLens != null)
                    {
                        AddRaySegment(reflectionLens);
                        CalculateNextRay(reflectionLens);
                    }

                    if (refractionLens != null)
                    {
                        AddRaySegment(refractionLens);
                        CalculateNextRay(refractionLens);
                    }
                    break;
            }
        }
        
        public RaySegment AddRaySegment(Vector3 origin, Vector3 endpoint, float intensity)
        {
            var rs = new RaySegment(origin, endpoint, intensity, _wavelength);
            _raySegments.Add(rs);
            return rs;
        }

        public void AddRaySegment(RaySegment rs)
        {
            _raySegments.Add(rs);
        }

        public RaySegment GetFirstClearRest()
        {
            if (_raySegments.Count == 0)
            {
                Debug.LogError("LightRoute can not consist of zero rays!");
                return null;
            }

            for (int i = 1; i < _raySegments.Count; i++)
            {
                var tmpSegment = _raySegments[i];
                tmpSegment.DestroyRaySegment();
                _raySegments.Remove(tmpSegment);

            }
            return _raySegments[0];
        }
        

    }
}
