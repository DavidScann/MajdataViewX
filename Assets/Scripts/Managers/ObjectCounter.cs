using UnityEngine;

using static MajdataViewX.Base.MajCtx;

namespace MajdataViewX.Managers
{
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
            if (_timeProvider.IsStart)
                UpdateOutput();
        }

        private void OnDestroy()
        {
            if (reportRequests.IsCreated) reportRequests.Dispose();
            outputBuilder.Dispose();
        }
    }
}