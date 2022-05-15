

using System;
using UnityEngine;
using UnityEngine.Serialization;

public class InputStickProcessor : MonoBehaviour
{
    public enum StickAccelMode
    {
        None,
        MoveTowards,
        SmoothDamp,
    }
    
    public enum AccelDampLerp
    {
        Constant,  // idk
        Magnitude, // smooth..? nah
        Distance,  // snappiest and most precise! my favorite :D
        DistanceInvert,
    }
    public enum AccelEvaluateMode
    {
        Magnitude,
        PerAxis,
    }
    
    // https://github.com/keijiro/OneEuroFilter
    [Serializable] public struct OneEuro
    {
        public bool enabled;
        public float beta;
        public float minCutoff;
    }

    // TODO: Stick smooth path/points interpolation (bezier-curve-like?)
    // Acceleration is very tricky to get to feel just right. It is needed for fast turn around, like with mouse. Slow constant turn speed is no fun
    public float sensitivity;
    public float damping;
    public float deadzone;
    public bool deadzoneLinear;
    public OneEuro oneEuroFilter;
    public float accel;
    [Tooltip("How fast max acceleration is reached. (MoveTowards)\n" + "X - In / Raise, Y - Out / Overshoot")]
    public Vector2 accelInOutSpeed; // x - in, y - out. higher damp - slower
    [Tooltip("How fast max acceleration is reached. (SmoothDamp)\n" + "Higher Damp - Slower." + "\n" + "X - In / Raise, Y - Out / Overshoot")]
    public Vector2 accelInOutDamp;
    [Tooltip("How damp is interpolated when acceleration direction overshoots desired direction.")]
    public StickAccelMode accelMode;
    public AccelDampLerp accelDampLerp;
    public AccelEvaluateMode accelEvaluateMode;
    public AnimationCurve accelCurve;
    public AnimationCurve magnitudeCurve;
    public AnimationCurve dampingCurve;
    public AnimationCurve sensitivityCurve;
    float dampLerp;
    float dampDelta;
    (float t, Vector2 x, Vector2 dx) oneEuroState;
    
    public void ApplyDeadzone(ref Vector2 v)
    {
        if (v.magnitude < deadzone) v = default;
        if (deadzoneLinear)
            v *= (v.magnitude - deadzone) / (1 - deadzone);
    }
    
    [Serializable] public struct Stick
    {
        public float x => value.x;
        public float y => value.x;
        public Vector2 value;
        public Vector2 filtered;
        public Vector2 raw;
        public Vector2 vel;
        public Vector2 accel;
        public Vector2 accelVel;
        public static implicit operator Vector2(Stick stick) => stick.value;
    }
    
    public void ProcessStick(ref Stick stick, float userAccel)
    {
        var dt = Time.deltaTime;
        ref var raw = ref stick.raw;
        ref var vel = ref stick.vel;
        ref var accel = ref stick.accel;
        ref var accelVel = ref stick.accelVel;
        ref var value = ref stick.value;
        ref var filtered = ref stick.filtered;
        
        ApplyDeadzone(ref raw);
        var targetValue = raw;

        if (accelMode != StickAccelMode.None)
        { 
            var stickMag = raw.magnitude; // raise accel gradually
            var accelDir = raw.normalized * magnitudeCurve.Evaluate(stickMag); // aspect affects the normalized direction

            // Problem 1: if we turn with max accel, and make a slight look up-down, it'll jump like crazy, as we're already at max 
            // Problem 2: fast turn around in the opposite direction causes accel to reset. Fix with "overshoot damp"

            if (accelMode != StickAccelMode.None)
            {
                dampLerp = accelDampLerp switch
                {
                    // reset acceleration faster when it overshoots desired direction, so there is no jump from slight adjustment after fast turn around
                    AccelDampLerp.Constant => accel.magnitude < accelDir.magnitude ? 0f : 1f,
                    AccelDampLerp.Magnitude => dampingCurve.Evaluate(accel.magnitude - accelDir.magnitude),
                    AccelDampLerp.Distance => dampingCurve.Evaluate(Vector2.Distance(accel, accelDir)),
                    AccelDampLerp.DistanceInvert => dampingCurve.Evaluate(1 - Vector2.Distance(accel, accelDir)),
                };
                var smoothDamp = accelMode == StickAccelMode.SmoothDamp;
                
                var speed = smoothDamp ? accelInOutDamp : accelInOutSpeed;
                dampDelta = Mathf.Lerp(speed.x, speed.y, dampLerp);
                
                accel = smoothDamp ? 
                    Vector2.SmoothDamp(accel, accelDir, ref accelVel, dt * dampDelta, 100, dt) : // higher delta - slower
                    Vector2.MoveTowards(accel, accelDir, dt * dampDelta); // higher delta - faster
            }
            
            if (accelEvaluateMode == AccelEvaluateMode.Magnitude)
                targetValue *= 1 + accelCurve.Evaluate(accel.magnitude) * this.accel * userAccel;
            else
                targetValue *= Vector2.one + this.accel * userAccel * new Vector2(
                    accelCurve.Evaluate(Mathf.Abs(accel.x)), 
                    accelCurve.Evaluate(Mathf.Abs(accel.y))
                    );
        }
        
        targetValue *= sensitivity * sensitivityCurve.Evaluate(raw.magnitude);
        
        filtered = oneEuroFilter.enabled ? OneEuroStep(Time.time, targetValue) : targetValue;
        
        if (damping < 1000)
        {
            // https://www.reddit.com/r/Unity3D/comments/ayf8rq/a_simple_follow_script_using_damped_harmonic/
            var n1 = vel - (value - filtered) * (damping * damping * dt);
            var n2 = 1 + damping * dt;
            vel = n1 / (n2 * n2);
            
            value += vel * dt;
        }
        else
            value = filtered;
    }
    
    Vector2 OneEuroStep(float t, Vector2 x)
    {
        var t_e = t - oneEuroState.t;

        // Do nothing if the time difference is too small.
        if (t_e < 1e-5f) 
            return oneEuroState.x;

        var dx = (x - oneEuroState.x) / t_e;
        var dx_res = Vector2.Lerp(oneEuroState.dx, dx, Alpha(t_e, DCutOff));

        var cutoff = oneEuroFilter.minCutoff + oneEuroFilter.beta * (dx_res.magnitude);
        var x_res = Vector2.Lerp(oneEuroState.x, x, Alpha(t_e, cutoff));

        oneEuroState = (t, x_res, dx_res);

        return x_res;
    }

    const float DCutOff = 1.0f;

    static float Alpha(float t_e, float cutoff)
    {
        var r = 2 * Mathf.PI * cutoff * t_e;
        return r / (r + 1);
    }
}