using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


namespace MajdataViewX.Managers
{
    public class ButtonsManager : MonoBehaviour
    {
        [SerializeField]
        private Dropdown ddResolution;

        private const int FULLSCREEN_WIDTH = 512;
        private const int FULLSCREEN_HEIGHT = 512;

        private void Start()
        {
            ddResolution.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ToggleFullscreen();
        }

        public void ToggleFullscreen()
        {
            Debug.Log("ToggleFullScreen");
            var resolutions = Screen.resolutions;
            if (Screen.fullScreen)
            {
                Screen.SetResolution(FULLSCREEN_WIDTH, FULLSCREEN_HEIGHT, false);
            }
            else
            {
                Screen.SetResolution(resolutions[^1].width, resolutions[^1].height, true);
            }

            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        public void DisplayDropdown()
        {
            ddResolution.value = 999;
            ddResolution.gameObject.SetActive(true);
        }

        public void SetResolution()
        {
            var i = ddResolution.value;
            Debug.Log(i);
            switch (i)
            {
                case 0:
                    Screen.SetResolution(512, 512, false);
                    break;
                case 1:
                    Screen.SetResolution(1080, 1080, false);
                    break;
                case 2:
                    Screen.SetResolution(1280, 720, false);
                    break;
                case 3:
                    Screen.SetResolution(1920, 1080, false);
                    break;
                case 4:
                    Screen.SetResolution(2560, 1440, false);
                    break;
                case 5:
                    Screen.SetResolution(3840, 2160, false);
                    break;
            }

            ddResolution.gameObject.SetActive(false);
        }
    }
}
