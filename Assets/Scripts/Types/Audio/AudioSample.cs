using ManagedBass;
using System;

namespace MajdataViewX.Types.Audio
{
    public class AudioSample : IDisposable
    {
        public SampleType SampleType { get; set; }

        /// <summary>
        /// HSAMPLE：整段 PCM decode 后驻留内存的 sample 资源句柄。
        /// 所有播放通道共享同一份 PCM 数据，因此 seek 是样本级精确的
        /// （无需 stream 的 Prescan）。
        /// </summary>
        private readonly int _handle;

        /// <summary>
        /// 持久控制通道：构造时取出并长期持有，<see cref="Play"/>/<see cref="Pause"/>/
        /// <see cref="Stop"/>/<see cref="CurrentSec"/>/<see cref="Volume"/>/<see cref="Speed"/>
        /// 全部作用于它。<see cref="PlayOneShot"/> 不改写本字段（它另取临时通道）。
        /// </summary>
        private readonly int _mainChannel;

        private float _volume;
        private double _length;
        private float _baseFrequency;

        public double CurrentSec
        {
            get => Bass.ChannelBytes2Seconds(
                _mainChannel,
                Bass.ChannelGetPosition(_mainChannel));
            set => Bass.ChannelSetPosition(
                _mainChannel,
                Bass.ChannelSeconds2Bytes(_mainChannel, value));
        }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 2f);

                if (_mainChannel != 0)
                    Bass.ChannelSetAttribute(
                        _mainChannel,
                        ChannelAttribute.Volume,
                        _volume);
            }
        }

        public float Speed
        {
            get =>
                (float)Bass.ChannelGetAttribute(
                    _mainChannel,
                    ChannelAttribute.Frequency) / _baseFrequency;

            set =>
                Bass.ChannelSetAttribute(
                    _mainChannel,
                    ChannelAttribute.Frequency,
                    _baseFrequency * value);
        }

        public double Length => _length;

        public PlaybackState State => Bass.ChannelIsActive(_mainChannel);

        public bool IsPlaying => State == PlaybackState.Playing;

        /// <param name="max">最大并发播放数（<see cref="PlayOneShot"/> 重叠触发用）。</param>
        public AudioSample(string file, int max = 1)
        {
            _handle = Bass.SampleLoad(file, 0, 0, max, BassFlags.SampleOverrideLongestPlaying);
            _mainChannel = Bass.SampleGetChannel(_handle);

            _length = Bass.ChannelBytes2Seconds(
                _mainChannel,
                Bass.ChannelGetLength(_mainChannel));
            _baseFrequency =
                (float)Bass.ChannelGetAttribute(
                    _mainChannel,
                    ChannelAttribute.Frequency);
        }

        /// <summary>
        /// 控制持久通道：从当前位置继续播放（resume 语义，不重置位置）。
        /// 用于需要持续控制的单通道播放（如 track 的播放/暂停/续播/seek）。
        /// 与 <see cref="PlayOneShot"/> 表现不同，见其说明。
        /// </summary>
        public void Play()
        {
            Bass.ChannelPlay(_mainChannel, false);
        }

        public void Pause()
        {
            Bass.ChannelPause(_mainChannel);
        }

        public void Stop()
        {
            Bass.ChannelStop(_mainChannel);
        }

        /// <summary>
        /// 打点式一次性播放：另取一个通道从头播放（restart），支持同一 sample 多次重叠触发，
        /// 用于 SFX。本方法不改写 <see cref="_mainChannel"/>。
        /// <para>
        /// 注意 <see cref="PlayOneShot"/> 与 <see cref="Play"/>/<see cref="Stop"/>/
        /// <see cref="Pause"/>/<see cref="CurrentSec"/> 的表现不同：
        /// <list type="bullet">
        /// <item><see cref="Play"/>/<see cref="Stop"/>/<see cref="Pause"/>/<see cref="CurrentSec"/>/
        /// <see cref="Volume"/>/<see cref="Speed"/> 只作用于持久通道 <see cref="_mainChannel"/>。</item>
        /// <item><see cref="PlayOneShot"/> 另起的临时通道不受上述控制影响。max=1 时临时通道
        /// 会复用 <see cref="_mainChannel"/>，此时 <see cref="PlayOneShot"/> 会打断
        /// <see cref="_mainChannel"/> 的当前播放，<see cref="Stop"/>/<see cref="Pause"/> 也能停掉它。</item>
        /// </list>
        /// 因此对 max&gt;1 的打点 SFX，不要用 <see cref="Stop"/>/<see cref="Pause"/> 去试图停止
        /// <see cref="PlayOneShot"/> 已触发的声音。
        /// </para>
        /// </summary>
        public void PlayOneShot()
        {
            var ch = Bass.SampleGetChannel(_handle);
            Bass.ChannelSetAttribute(
                ch,
                ChannelAttribute.Volume,
                _volume);
            Bass.ChannelPlay(ch, true);
        }

        public void Dispose()
        {
            Bass.SampleFree(_handle);
        }
    }
}