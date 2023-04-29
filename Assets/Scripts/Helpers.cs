using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Helpers
{
    public static float Map(float value, float minIn, float maxIn, float minOut, float maxOut)
    {
        return (value - minIn) / (maxIn - minIn) * (maxOut - minOut) + minOut;
    }

    public static float Map(float value, float minIn, float maxIn, float minOut, float maxOut, bool clamp)
    {
        float mappedValue = (value - minIn) / (maxIn - minIn) * (maxOut - minOut) + minOut;
        if (clamp) return Mathf.Clamp(mappedValue, Mathf.Min(minOut,maxOut), Mathf.Max(minOut, maxOut));
        else return mappedValue;
    }
}

public class Stopwatch
{
    //Used to measure time for code to finish

    DateTime start;
    DateTime split;

    public Stopwatch()
    {
        start = DateTime.Now;
        split = start;
    }

    public string Lap()
    {
        DateTime current = DateTime.Now;
        TimeSpan time = current.Subtract(split);
        split = current;
        return time.Milliseconds + " ms";
    }
    public string Stop()
    {
        DateTime current = DateTime.Now;
        TimeSpan time = current.Subtract(start);
        return time.Milliseconds + " ms";
    }
}