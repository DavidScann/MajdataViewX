using MajdataViewX.Managers;
using MajdataViewX.Notes.NoteDatas;
using MajdataViewX.Notes.SlideUtils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;
using Unity.Mathematics;
using static UnityEngine.Rendering.DebugUI;

namespace MajdataViewX.Types.Input
{
    /// <summary>
    /// 标记一次DJAuto的操作，为了省内存和凑完美32字节真的是各种诡异操作
    /// 至少符合我的代码洁癖，调用时注意做好类型区分
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal unsafe struct DJAutoPlayData
    {
        // x64 point = 8 bytes

        /// <summary>
        /// 标记这个PlayData是什么类型的
        /// </summary>
        [FieldOffset(0)]
        public readonly DJAutoPlayType Type;


        [FieldOffset(1)]
        private byte _flag;
        /// <summary>
        /// 标记这个swipe是否是由wifi产生的，如果是，则需要±22.5度的偏移变成滑动的双手
        /// </summary>
        public readonly bool IsWifi
        {
            get => (_flag & 0b_0000_0001) != 0;
            private init => _flag = (byte)((_flag & ~0b_0000_0001) | (value ? 0b_0000_0001 : 0));
        }
        /// <summary>
        /// 手认领：FindNext 认领的散点 play 置此位，FindNext 跳过；ResetLoadedPlay 也用它把 ignore 之前的
        /// play 标为忽略（保留全部数据）；运行时状态，重开/seek 时由 <see cref="NoteManager.ResetLoadedPlay"/> 重建。
        /// </summary>
        public bool IsHandClaimed
        {
            readonly get => (_flag & 0b_0000_0010) != 0;
            set => _flag = (byte)((_flag & ~0b_0000_0010) | (value ? 0b_0000_0010 : 0));
        }
        /// <summary>
        /// 被算法分配：加载期由 BindPlayPatterns（绑定后继）与 BindSkippableHitsBySwipe（可被 swipe 蹭掉）置位，
        /// 仅作加载期记录（绑定跟随仍由 BindPlayOffset 驱动）；FindNext 不再据此跳过，静态数据随 chart 重载重建。
        /// </summary>
        public bool IsAssigned
        {
            readonly get => (_flag & 0b_0000_1000) != 0;
            set => _flag = (byte)((_flag & ~0b_0000_1000) | (value ? 0b_0000_1000 : 0));
        }
        /// <summary>
        /// 是否允许被 swipe 顺带覆盖（tap/hold）
        /// </summary>
        public readonly bool IsAllowSkipBySwipe
        {
            get => (_flag & 0b_0000_0100) != 0;
            private init => _flag = (byte)((_flag & ~0b_0000_0100) | (value ? 0b_0000_0100 : 0));
        }

        /// <summary>
        /// 有相关联的play，<see cref="NoteManager.BindPlayPatterns"/>
        /// 注意绑定后是在On状态下移动到对应play
        /// </summary>
        [FieldOffset(2)]
        public ushort BindPlayOffset;


        // ======hit======

        /// <summary>
        /// 世界坐标
        /// 利用大于4.8半径的位置按下判定为按下外键的特性支持外键打击
        /// </summary>
        [FieldOffset(4)]
        public readonly float2 Pos;

        /// <summary>
        /// 手的半径
        /// radius后面可能为了蹭touch要改一下
        /// </summary>
        [FieldOffset(12)]
        public float Radius;

        /// <summary>
        /// 起始时间，也就是开始按下的时间（拍划绑定的 swipe 会加入一定延迟）
        /// </summary>
        [FieldOffset(16)]
        public float StartTime;
        /// <summary>
        /// 结束时间，也就是放手的时间（拍划绑定的 hit 会被改成 swipe.StartTime 以无缝转手）
        /// </summary>
        [FieldOffset(20)]
        public float EndTime;

        /// <summary>
        /// 当这是一个slide头（star tap）时，它的slide部分的swipe
        /// </summary>
        [FieldOffset(24)]
        public int SlideSwipe;




        // ======swipe======

        /// <summary>
        /// 标记这个swipe是准备去蹭C区touch的，别的区算法会解决，C区距离实在太远，单纯扩大半径已经不合适了，-1为没绑上
        /// </summary>
        [FieldOffset(4)]
        public float SkipCTime;

        /// <summary>wifi 双手偏移方向：+1 = +11.25°，-1 = -11.25°。由 play 自带而非 handIdx 决定，避免强制左绑左右绑右。</summary>
        [FieldOffset(5)]
        public readonly sbyte WifiSide;

        // 6~8 bytes padding

        // 前面同定义
        //[FieldOffset(12)]
        //public float Radius;
        //[FieldOffset(16)]
        //public readonly float StartTime;
        //[FieldOffset(20)]
        //public readonly float EndTime;

        /// <summary>
        /// 获取到slide arrows的指针之前先存索引，然后统一赋值，防止指针悬置
        /// </summary>
        [FieldOffset(24)]
        public readonly int BindSlideIndex;
        /// <summary>
        /// 直接负责获取到slide arrows的指针
        /// </summary>
        [FieldOffset(24)]
        public SlideData* BindSlide;



        /// <summary>
        /// Hit类型Play
        /// </summary>
        public DJAutoPlayData(
            float2 pos, float radius,
            float startTime, float endTime,
            bool isAllowSkipBySwipe)
        {
            this = default;
            Type = DJAutoPlayType.Hit;
            Pos = pos;
            Radius = radius;
            StartTime = startTime;
            EndTime = endTime;
            IsAllowSkipBySwipe = isAllowSkipBySwipe;
        }

        /// <summary>
        /// Swipe类型Play
        /// </summary>
        public DJAutoPlayData(
            int bindingSlideIndex,
            float radius, float startTime, float endTime,
            bool isWifi, sbyte wifiSide = -1)
        {
            this = default;
            Type = DJAutoPlayType.Swipe;
            BindSlideIndex = bindingSlideIndex;
            Radius = radius;
            StartTime = startTime;
            EndTime = endTime;
            IsWifi = isWifi;
            WifiSide = wifiSide;
        }


        public readonly bool IsReleased(float time)
        {
            bool released = time > EndTime;
            if (Type is DJAutoPlayType.Swipe)
            {
                released |=
                    BindSlide->isEnd ||
                    BindSlide->isSlideEnd ||
                    (BindSlide->isJudged && time > BindSlide->judgeTime + NoteManager.DJAUTO_SLIDE_RELEASE_DELAY_SEC);
            }
            return released;
        }

        public readonly float2 GetEntryPos()
        {
            if (Type is DJAutoPlayType.Hit)
            {
                return Pos;
            }
            else if (Type is DJAutoPlayType.Swipe)
            {
                var arrows = BindSlide->slideArrows;
                return new float2(arrows[0].X, arrows[0].Y);
            }
            else
                return float2.zero;
        }

        public readonly float2 GetEndPos()
        {
            if (Type is DJAutoPlayType.Hit)
            {
                return Pos;
            }
            else if (Type is DJAutoPlayType.Swipe)
            {
                var arrows = BindSlide->slideArrows;
                var count = BindSlide->slideArrowsCount;
                return new float2(arrows[count - 1].X, arrows[count - 1].Y);
            }
            else
                return float2.zero;
        }

        /// <summary>FindNext 分配参考点：非 wifi 返回入口；wifi 返回偏移后中点，以区分 side 归属--哪只手离某 side 中点近就拿该 side。</summary>
        public readonly float2 GetAssignPos()
        {
            if (Type is not DJAutoPlayType.Swipe || !IsWifi) return GetEntryPos();
            var arrows = BindSlide->slideArrows;
            var count = BindSlide->slideArrowsCount;
            var startPos = new float2(arrows[0].X, arrows[0].Y);
            var midPos = new float2(arrows[(int)(count * 0.5)].X, arrows[(int)(count * 0.5)].Y);
            var offset = midPos - startPos;
            var rad = math.radians(11.25f);
            var cos = math.cos(rad);
            var sin = math.sin(rad);
            var side = WifiSide;
            return startPos + new float2(
                offset.x * cos - side * offset.y * sin,
                side * offset.x * sin + offset.y * cos);
        }

        public readonly float2 GetCurPos(float time)
        {
            if (Type is not DJAutoPlayType.Swipe) return Pos;
            var arrows = BindSlide->slideArrows;
            var count = BindSlide->slideArrowsCount;
            var startTime = StartTime;
            var endTime = EndTime;

            if (count <= 1 || arrows == null) return float2.zero;
            var duration = endTime - startTime;
            var progress = duration > 0f ? math.saturate((time - startTime) / duration) : 0f;
            int idxLast = count - 1;
            var distance = progress * arrows[idxLast].L;
            int processIdx = 1;
            while (processIdx < idxLast && arrows[processIdx].L < distance) processIdx++;
            var p0 = arrows[processIdx - 1];
            var p1 = arrows[processIdx];
            var t = math.unlerp(p0.L, p1.L, distance);
            var pos = new float2(math.lerp(p0.X, p1.X, t), math.lerp(p0.Y, p1.Y, t));
            if (SkipCTime != -1)
            {
                if (math.abs(time - SkipCTime) <= 0.01f)
                {
                    // 蹭C区touch
                    return pos * 0.56f;
                }
            }
            if (IsWifi)
            {
                var side = WifiSide;
                var posC = pos;
                var startPos = new float2(arrows[0].X, arrows[0].Y);
                var offset = posC - startPos;
                var rad = math.radians(11.25f);
                var cos = math.cos(rad);
                var sin = math.sin(rad);
                return startPos + new float2(
                    offset.x * cos - side * offset.y * sin,
                    side * offset.x * sin + offset.y * cos);
            }
            return pos;
        }
    }

    internal enum DJAutoPlayType : byte
    {
        NoneOrFinished,
        Hit,
        Swipe
    }
}
