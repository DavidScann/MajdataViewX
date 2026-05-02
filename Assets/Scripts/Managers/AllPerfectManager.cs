#nullable enable

#region

using Cysharp.Threading.Tasks;
using UnityEngine;

#endregion

public class AllPerfectManager : MonoBehaviour
{
    [SerializeField]
    Animator AllPerfect;

    private bool isPlayed;
    
    private void Awake()
    {
        Majdata<AllPerfectManager>.Instance = this;
        AllPerfect.gameObject.SetActive(false);
    }

    private void Start()
    {
        AllPerfect.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (PlayManager.Summary.State is not ViewStatus.Playing)
            return;

        if (Majdata<ObjectCounter>.Instance!.AllFinished)
        {
            if (isPlayed)
            {
                if (!AllPerfect.gameObject.activeSelf)
                    Majdata<PlayManager>.Instance!.StopAsync().Forget();
            }
            else
            {
                AllPerfect.gameObject.SetActive(true);
                AllPerfect.SetTrigger("playAllPerfect");
                isPlayed = true;
            }
        }
    }

    public void ResetState()
    { 
        AllPerfect.gameObject.SetActive(false);
        isPlayed = false;
    }
}