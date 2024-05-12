using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothValue
{
    float _value;
    public float value
    {
        get {return _value;}
    }
    float target;
    float smoothness;
    float min;
    float max;

    public SmoothValue(float initial, float min, float max, float smoothness)
    {
        this._value = initial;
        this.target = initial;
        this.smoothness = Mathf.Clamp(smoothness, 0f, 1f);
        this.min = min;
        this.max = max;
    }

    // Call it every frame! returns updated value
    public float Update(float target, float deltaTime)
    {
        this.target = Mathf.Clamp(target, min, max);
        _value += (this.target - _value) * smoothness;
        return _value;
    }
}
