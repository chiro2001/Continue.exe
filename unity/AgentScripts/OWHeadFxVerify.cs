// 校验头部气氛 FX 的着色器与脚本是否就位。
// 通过 Pipeline 运行：
//   unity command run_script --file AgentScripts/OWHeadFxVerify.cs --entry OWHeadFxVerify.Run
// 该文件放在 Assets/ 之外，因此不会触发 Unity 资源导入。
using System;
using System.Linq;
using System.Reflection;

public static class OWHeadFxVerify
{
    public static object Run()
    {
        var atmosphere = UnityEngine.Shader.Find("OldWebMap/Head Atmosphere Overlay");
        var signal = UnityEngine.Shader.Find("OldWebMap/Head Signal Noise");

        var rigType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("OldWebMap.OWHeadOverlayRig"))
            .FirstOrDefault(t => t != null);

        string[] rigMethods = rigType == null
            ? new string[0]
            : new[] { "SetIntensity", "SetPulse", "FirePulse", "ApplyScale" }
                .Where(m => rigType.GetMethod(m, BindingFlags.Public | BindingFlags.Instance) != null)
                .ToArray();

        // HasProperty 在 Material 上；用临时材质读取，用完即销毁
        var atmosphereMat = atmosphere == null ? null : new UnityEngine.Material(atmosphere);
        var signalMat = signal == null ? null : new UnityEngine.Material(signal);
        var atmosphereProps = atmosphereMat == null ? new string[0] : new[]
        {
            "_Intensity", "_VignetteStrength", "_ScanlineStrength", "_Pulse", "_PulseFlash", "_Letterbox"
        }.Where(p => atmosphereMat.HasProperty(p)).ToArray();
        var signalProps = signalMat == null ? new string[0] : new[]
        {
            "_Intensity", "_SnowTex", "_DropAmount", "_RollAmount", "_GapAmount"
        }.Where(p => signalMat.HasProperty(p)).ToArray();
        if (atmosphereMat != null) UnityEngine.Object.DestroyImmediate(atmosphereMat);
        if (signalMat != null) UnityEngine.Object.DestroyImmediate(signalMat);

        return new
        {
            atmosphereFound = atmosphere != null,
            atmosphereProperties = atmosphereProps,
            signalFound = signal != null,
            signalProperties = signalProps,
            rigTypeFound = rigType != null,
            rigMethods,
            ok = atmosphere != null && signal != null && rigType != null && rigMethods.Length == 4
        };
    }
}
