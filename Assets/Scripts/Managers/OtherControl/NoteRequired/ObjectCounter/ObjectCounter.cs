using UnityEngine;

using static MajCtx;

public partial class ObjectCounter : MonoBehaviour
{
    private void Awake()
    {
        _objectCounter = this;
    }

    private void Start()
    {
        ResetState();
    }

    private void Update()
    {
        // ProcessReportRequests(); // in NoteManager, after job complete
        UpdateOutput();
    }

    private void OnDestroy()
    {
        if (reportRequests.IsCreated) reportRequests.Dispose();
        outputBuilder.Dispose();
    }
}
