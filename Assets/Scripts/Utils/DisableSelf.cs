#region

using UnityEngine;

#endregion

public class DisableSelf : MonoBehaviour
{
    public bool disableSelf;
    public void Update()
    {
        if (disableSelf)
            gameObject.SetActive(false);
    }
}