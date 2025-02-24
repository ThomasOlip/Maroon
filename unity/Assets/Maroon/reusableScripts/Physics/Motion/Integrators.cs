using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Maroon.Physics.Motion
{
    /// <summary>
    /// Explicit Euler Integrator
    /// </summary>
    public class ExplicitEuler : IIntegrator
    {
        public MotionState Integrate(MotionState s, double t, double dt)
        {
            s.position += s.velocity * dt;
            s.velocity += s.EvaluateAccelerationAt(t + dt) * dt;

            return s;
        }
    }


    /// <summary>
    /// Semi-Implicit Euler Integrator
    /// </summary>
    public class SemiImplicitEuler : IIntegrator
    {
        public MotionState Integrate(MotionState s, double t, double dt)
        { 
            s.velocity += s.EvaluateAccelerationAt(t + dt) * dt;
            s.position += s.velocity * dt;

            return s;
        }
    }


    /// <summary>
    /// 4th Order Runge-Kutta Integrator
    /// </summary>
    public class RungeKutta4 : IIntegrator
    {
        public MotionState Integrate(MotionState s, double t, double dt)
        {
            var k1 = Evaluate(s, t, 0.0, new Derivative());
            var k2 = Evaluate(s, t, dt * 0.5, k1);
            var k3 = Evaluate(s, t, dt * 0.5, k2);
            var k4 = Evaluate(s, t, dt, k3);

            var dxdt = 1.0 / 6.0 * (k1.dx + 2.0 * k2.dx + 2.0 * k3.dx + k4.dx);
            var dvdt = 1.0 / 6.0 * (k1.dv + 2.0 * k2.dv + 2.0 * k3.dv + k4.dv);

            s.position += dxdt * dt;
            s.velocity += dvdt * dt;
            s.EvaluateAccelerationAt(t + dt);

            return s;
        }

        private Derivative Evaluate(MotionState initial, double t, double dt, Derivative d)
        {
            MotionState s = new MotionState(initial, true);
            s.position += d.dx * dt;
            s.velocity += d.dv * dt;
            return new Derivative(s.velocity, s.EvaluateAccelerationAt(t + dt));
        }

        private class Derivative
        {
            public Vector3d dx; // velocity
            public Vector3d dv; // acceleration

            public Derivative(Vector3d dx, Vector3d dv)
            {
                this.dx = dx;
                this.dv = dv;
            }

            public Derivative() { }
        }
    }

    /// <summary>
    /// Velocity Verlet Integrator
    /// </summary>
    public class VelocityVerlet : IIntegrator
    {
        public MotionState Integrate(MotionState s, double t, double dt)
        {
            s.position += s.velocity * dt + s.acceleration * dt * dt * 0.5;
            s.velocity += (s.acceleration + s.EvaluateAccelerationAt(t + dt)) * dt * 0.5;

            return s;
        }
    }
}