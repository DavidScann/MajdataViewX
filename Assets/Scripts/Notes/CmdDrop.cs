using System;
using UnityEngine;
#nullable enable
namespace Assets.Scripts.Notes
{
    /// <summary>
    /// 命令，在指定时间点触发指定事件，当times小于0时持续触发，大于0时触发指定次数，等于0时不触发直接销毁（=Destroy）
    /// </summary>
    public class CmdDrop : NoteDrop
    {
        public Action Handler;
        public int times;

        private int ptimes = 0;
        private AudioTimeProvider timeProvider;

        private void Start()
        {
            timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        }

        protected void FixedUpdate()
        {
            while (timeProvider == null) timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
            var realtime = timeProvider.AudioTime - time;
            if (realtime >= -0.01f)
            {
                if (times < 0)
                {
                    Handler();
                }
                else if (times == 0)
                {
                    Destroy(gameObject);
                }
                else
                {
                    for (; ptimes < times; ptimes++)
                    {
                        Handler();
                    }
                    Destroy(gameObject);
                }
            }
        }
    }
}