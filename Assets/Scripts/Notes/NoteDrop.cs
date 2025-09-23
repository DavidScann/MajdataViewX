using UnityEngine;

public class NoteDrop : MonoBehaviour
{
    public float time;
    public int noteSortOrder;
    public bool isUnplayable;
    public int canSVAffect;
}

public class NoteLongDrop : NoteDrop
{
    public float LastFor = 1f;
}