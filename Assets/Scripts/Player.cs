using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player
{
    public static Player current;

    public int playerNum;
    public Vector3 ballPosition;
    public int[] stroke;
    public int hole;

    public Player(int id, Vector3 startPos, int holeCount)
    {
        playerNum = id;
        ballPosition = startPos;
        stroke = new int[holeCount];
        hole = 0;
    }

    public int CurrentStroke()
    {
        return stroke[hole];
    }

    public int TotalStrokes()
    {
        int t = 0;

        for(int i = 0; i < stroke.Length; i++)
        {
            t += stroke[i];
        }

        return t;
    }
}
