// DJAutoEval — headless DJAuto ground-truth evaluation against a real chart.
//
// Usage (Unity batch mode, no graphics needed):
//   Unity -batchmode -nographics -quit -projectPath <repo> \
//     -executeMethod DJAutoEval.Run \
//     -chartPath /path/to/maidata.txt \
//     -fps 60 -mode 2
//
// mode: 0=Enable 1=DJAutoButton 2=DJAutoSensor 3=Random 4=Disable
//
// Parses the maidata, builds the note arrays with the real loader, then steps
// the DJAutoSim at the given render FPS (240Hz sim clock inside), replicating
// the game's per-frame BeginHandler + sim Step. Dumps the grade distribution
// per note type plus any notes the sim never judged (they'd be misses).

using System;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MajdataViewX.Base;
using MajdataViewX.Managers;
using MajdataViewX.Notes;
using MajdataViewX.Notes.NoteDatas;
using MajdataViewX.Notes.SlideUtils;
using MajdataViewX.Types.Enums;
using MajdataViewX.Types.Input;
using MajdataViewX.Types.Notes;
using MajdataViewX.Types.Notes.RenderData;
using MajdataViewX.Utils;
using MajSimai;

public static class DJAutoEval
{
    static string GetArg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }

    static FieldInfo F(string name) =>
        typeof(NoteManager).GetField(name,
            BindingFlags.NonPublic | BindingFlags.Instance);

    public static unsafe void Run()
    {
        var chartPath = GetArg("-chartPath",
            "/home/davidscann/Videos/maisync_neo/輪廻玲々 (Rinne Reirei)/maidata.txt");
        var fps = int.Parse(GetArg("-fps", "60"));
        var mode = (AutoPlayMode)int.Parse(GetArg("-mode", "2"));
        var wide = GetArg("-wide", "0") == "1";

        Debug.Log($"[eval] chart={chartPath} fps={fps} mode={mode}");

        // ---- extract the fullest &inote_ section (real chart, not placeholders) ----
        var lines = File.ReadAllLines(chartPath);
        var bestIdx = -1; var bestLen = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].TrimStart().StartsWith("&inote_")) continue;
            var len = 0;
            for (var j = i + 1; j < lines.Length && !lines[j].TrimStart().StartsWith("&"); j++)
                len += lines[j].Length;
            if (len > bestLen) { bestLen = len; bestIdx = i; }
        }
        if (bestIdx < 0) { Debug.LogError("[eval] no &inote_ section"); return; }

        var level = ""; var designer = "";
        foreach (var l in lines)
        {
            if (l.StartsWith("&lv_5")) level = l.Split('=')[1].Trim();
            else if (l.StartsWith("&des")) designer = l.Split('=')[1].Trim();
        }
        var sb = new StringBuilder();
        var eq = lines[bestIdx].IndexOf('=');
        sb.AppendLine(lines[bestIdx].Substring(eq + 1));
        for (var i = bestIdx + 1; i < lines.Length && !lines[i].TrimStart().StartsWith("&"); i++)
            sb.AppendLine(lines[i]);

        var chart = SimaiParser.ParseChartAsync(level, designer, sb.ToString())
            .GetAwaiter().GetResult();
        var noteCount = 0;
        float lastTime = 0;
        foreach (var timing in chart.NoteTimings)
        {
            noteCount += timing.Notes.Length;
            lastTime = Mathf.Max(lastTime, (float)timing.Timing);
        }
        Debug.Log($"[eval] parsed: {chart.NoteTimings.Length} timing points, {noteCount} notes, last={lastTime:F2}s");
        Debug.Log($"[eval] fumen head: {sb.ToString().Substring(0, Mathf.Min(200, sb.Length)).Replace("\n", "\\n")}");

        // ---- statics (mirror PlayManager.Start) ----
        SlideTableNeo.InitializeStandardSlideTable();
        MajBurst.__DataSS.Data = new MajBurstData
        {
            TimeData = new(),
            InputData = new(),
            MultTouchHandler = new(),
            GlobalRandom = new(12345),
        };
        MajBurst.InputData.Init();
        MajBurst.MultTouchHandler.Init();
        NoteHelper.AutoPlayModeSS.Data = mode;
        InputManager.WideHands = wide;
        // InputManager's constructor fills the sensor world positions; the harness has none, so set them manually
        // (else GetSensorMask uses all-zero coordinates and no cover ever covers a sensor).
        for (var i = 0; i < MajCtx.SENSOR_COUNT; i++)
            MajBurst.InputData.SensorWorldPositions[i] =
                MajdataViewX.Base.MajPos.GetSensorWorldPos((SensorType)i);

        // ---- build notes with the real loader ----
        var nmGo = new GameObject("EvalNoteManager");
        var nm = nmGo.AddComponent<NoteManager>();
        // Skip render-group setup (no graphics): pre-set the capacity multiplier
        // so ConfigureRenderCapacity early-returns for low-density charts.
        F("_renderCapacityMultiplier").SetValue(nm, 1);
        nm.Load(chart);

        var taps = (NativeList<TapData>)F("taps").GetValue(nm);
        var holds = (NativeList<HoldData>)F("holds").GetValue(nm);
        var slides = (NativeList<SlideData>)F("slides").GetValue(nm);
        var touches = (NativeList<TouchData>)F("touches").GetValue(nm);
        var touchHolds = (NativeList<TouchHoldData>)F("touchHolds").GetValue(nm);
        var tGroupCovers = (NativeList<CoverResult>)F("touchGroupCoverResults").GetValue(nm);
        var thGroupCovers = (NativeList<CoverResult>)F("touchHoldGroupCoverResults").GetValue(nm);
        var tGroupTotals = (NativeList<int>)F("touchGroupTotalCounts").GetValue(nm);
        var tGroupJudged = (NativeList<int>)F("touchGroupJudgedCounts").GetValue(nm);
        Debug.Log($"[eval] built: taps={taps.Length} holds={holds.Length} slides={slides.Length} touches={touches.Length} touchHolds={touchHolds.Length}");

        // ---- dummy sfx/effect/report sinks ----
        var sfx = new NativeArray<bool>(16, Allocator.Persistent);
        var fx = new NativeArray<EffectData>(256, Allocator.Persistent);
        var reports = new NativeList<ReportResultEntry>(65536, Allocator.Persistent);

        var sim = new DJAutoSim();
        var ctx = new DJAutoSim.SimContext
        {
            Taps = taps, Holds = holds, Slides = slides,
            Touches = touches, TouchHolds = touchHolds,
            TouchGroupCoverResults = tGroupCovers,
            TouchHoldGroupCoverResults = thGroupCovers,
            TouchGroupTotalCounts = tGroupTotals,
            TouchGroupJudgedCounts = tGroupJudged,
            SfxRequests = (bool*)sfx.GetUnsafePtr(),
            JudgeEffectRequests = (EffectData*)fx.GetUnsafePtr(),
            ReportResults = reports.AsParallelWriter(),
        };

        // ---- run: simulate render frames at `fps`, sim ticks 240Hz inside ----
        var dt = 1f / Mathf.Max(fps, 1);
        var tEnd = lastTime + 5f; // tail for window misses
        for (var t = -3f; t < tEnd; t += dt)
        {
            MajBurst.InputData.BeginHandler(false);
            sim.Step(t, ctx);
        }
        // final frames: flush everything
        for (var t = tEnd; t < tEnd + 0.5f; t += dt)
        {
            MajBurst.InputData.BeginHandler(false);
            sim.Step(t, ctx);
        }

        // ---- dump results ----
        Debug.Log($"[eval] === RESULTS fps={fps} mode={mode} ===");

        // The render-side slide-OK lifecycle reports a judged slide a few frames
        // later (EndSlide); replicate it here: judged slides count as reported.
        var lateSamples = new System.Collections.Generic.List<string>();
        var missSamples = new System.Collections.Generic.List<string>();
        int missTap = 0, missHold = 0, missSlide = 0, missTouch = 0, missTH = 0;
        int lateTap = 0, lateHold = 0, lateSlide = 0, lateTouch = 0, lateTH = 0;
        bool IsLate(JudgeGrade g) => g != JudgeGrade.FastCritical && g != JudgeGrade.LateCritical && g != JudgeGrade.Miss;

        foreach (var t in taps)
        {
            if (!t.IsEnd)
            {
                missTap++;
                if (missSamples.Count < 25) missSamples.Add($"tap t={t.Time:F2} key={(int)t.Key}");
            }
            else if (IsLate(t.JudgeGrade))
            {
                lateTap++;
                if (lateSamples.Count < 25)
                    lateSamples.Add($"tap t={t.Time:F2} key={(int)t.Key} grade={t.JudgeGrade} diff={t.Diff * 1000:F0}ms");
            }
        }
        foreach (var h in holds)
        {
            if (!h.isEnd) { missHold++; if (missSamples.Count < 25) missSamples.Add($"hold t={h.time:F2} key={(int)h.Key}"); }
            else if (IsLate(h.judgeGrade))
            {
                lateHold++;
                if (lateSamples.Count < 25)
                    lateSamples.Add($"hold t={h.time:F2} key={(int)h.Key} grade={h.judgeGrade}");
            }
        }
        foreach (var s in slides)
        {
            if (!s.isJudged) { missSlide++; if (missSamples.Count < 25) missSamples.Add($"slide t={s.shootTime:F2}"); }
            else if (IsLate(s.judgeGrade))
            {
                lateSlide++;
                if (lateSamples.Count < 25)
                    lateSamples.Add($"slide t={s.shootTime:F2} grade={s.judgeGrade}");
            }
        }
        foreach (var t in touches)
        {
            if (!t.isEnd) { missTouch++; if (missSamples.Count < 25) missSamples.Add($"touch t={t.time:F2} s={(int)t.sensor}"); }
            else if (IsLate(t.judgeGrade))
            {
                lateTouch++;
                if (lateSamples.Count < 25)
                    lateSamples.Add($"touch t={t.time:F2} s={(int)t.sensor} grade={t.judgeGrade} diff={t.diff*1000:F0}ms");
                if (t.coverageId >= 0 && t.coverageId < tGroupCovers.Length && t.judgeGrade == JudgeGrade.LateGood)
                {
                    var cov = tGroupCovers[t.coverageId];
                    var sp = MajdataViewX.Base.MajPos.GetSensorWorldPos(t.sensor);
                    var c1 = cov.Circle1.Center;
                    Debug.Log($"[eval] touch t={t.time:F2} s={(int)t.sensor} covMode={cov.Mode} " +
                              $"r1={cov.Circle1.Radius:F2} d1={UnityEngine.Mathf.Sqrt((sp.x-c1.x)*(sp.x-c1.x)+(sp.y-c1.y)*(sp.y-c1.y)):F2} " +
                              $"r2={cov.Circle2.Radius:F2}");
                }
            }
        }
        foreach (var t in touchHolds)
        {
            var hg = t.headGroupId;
            var hc = t.headCoverageId;
            var hcMode = hc >= 0 && hc < tGroupCovers.Length ? tGroupCovers[hc].Mode : CoverMode.None;
            var hcR = hc >= 0 && hc < tGroupCovers.Length ? tGroupCovers[hc].Circle1.Radius : 0f;
            if (!t.isEnd) { missTH++; if (missSamples.Count < 25) missSamples.Add($"touchhold t={t.time:F2} s={(int)t.sensor}"); }
            else if (IsLate(t.judgeGrade))
            {
                lateTH++;
                if (lateSamples.Count < 25)
                    lateSamples.Add($"touchhold t={t.time:F2} s={(int)t.sensor} grade={t.judgeGrade} diff={t.headDiff*1000:F0}ms");
            }
            Debug.Log($"[eval] TH t={t.time:F2} s={(int)t.sensor} hg={hg} hc={hc} cov={hcMode} r={hcR:F2} grade={t.judgeGrade}");
        }
        Debug.Log($"[eval] MISSES: tap={missTap} hold={missHold} slide={missSlide} touch={missTouch} touchhold={missTH} (total {missTap + missHold + missSlide + missTouch + missTH})");
        Debug.Log($"[eval] LATE(>critical): tap={lateTap} hold={lateHold} slide={lateSlide} touch={lateTouch} touchhold={lateTH} (total {lateTap + lateHold + lateSlide + lateTouch + lateTH})");
        foreach (var s in missSamples) Debug.Log($"[eval]   MISS {s}");
        foreach (var s in lateSamples) Debug.Log($"[eval]   LATE {s}");

        int[] byGrade = new int[Enum.GetValues(typeof(JudgeGrade)).Length];
        foreach (var r in reports) byGrade[(int)r.Grade]++;
        foreach (var s in slides) if (s.isJudged) byGrade[(int)s.judgeGrade]++;
        foreach (var g in Enum.GetValues(typeof(JudgeGrade)))
            if (byGrade[(int)g] > 0)
                Debug.Log($"[eval] grade {g}: {byGrade[(int)g]}");
        Debug.Log($"[eval] simTicks={sim.TotalTicks}");

        sfx.Dispose(); fx.Dispose(); reports.Dispose();
        Debug.Log("[eval] DONE");
    }
}
