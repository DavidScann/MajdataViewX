using UnityEngine;
#nullable enable

public class PlayAllPerfect : MonoBehaviour
{
    private GameObject Allperfect;
    private TimeProvider timeProvider;
    DataLoader loader;

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
        if (loader.State is not (NoteLoaderStatus.Idle or NoteLoaderStatus.Finished))
            return;
        
        if (timeProvider.isStart && transform.childCount == 0 && Allperfect) 
            Allperfect.SetActive(true);
    }
}