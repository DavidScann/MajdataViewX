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
        ProcessReportRequests();
        UpdateOutput();
    }

    private void OnDestroy()
    {
        if (reportRequests.IsCreated) reportRequests.Dispose();
        if (reportCount.IsCreated) reportCount.Dispose();
    }
}
