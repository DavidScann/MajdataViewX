using UnityEngine;
#nullable enable

public class PlayAllPerfect : MonoBehaviour
{
    private TimeProvider timeProvider;
    private DataLoader loader;

    private GameObject Allperfect;
    
    private void Awake()
    {
        Majdata<PlayAllPerfect>.Instance = this;
    }
    
    private void Start()
    {
        loader = Majdata<DataLoader>.Instance!;
        timeProvider = Majdata<TimeProvider>.Instance!;
        
        Allperfect = GameObject.Find("CanvasAllPerfect");
        Allperfect.SetActive(false);
    }

    // Update is called once per frame
    private void Update()
    {
        if (loader == null)
            return;
        if (PlayManager.Summary.State is not ViewStatus.Playing)
            return;
        
        if (timeProvider.isStart && transform.childCount == 0 && Allperfect) 
            Allperfect.SetActive(true);
    }
}