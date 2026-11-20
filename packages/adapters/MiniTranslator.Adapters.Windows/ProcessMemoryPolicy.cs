using System.Runtime;
using System.Runtime.InteropServices;

namespace MiniTranslator.Adapters.Windows;

public static class ProcessMemoryPolicy
{
    public static void SetEfficiencyMode(bool enabled)
    {
        var state = new NativeMethods.PROCESS_POWER_THROTTLING_STATE
        {
            Version = NativeMethods.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = enabled ? NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
        };

        _ = NativeMethods.SetProcessInformation(
            NativeMethods.GetCurrentProcess(),
            NativeMethods.ProcessPowerThrottling,
            ref state,
            Marshal.SizeOf<NativeMethods.PROCESS_POWER_THROTTLING_STATE>());
    }

    public static void TrimWorkingSet()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        _ = NativeMethods.SetProcessWorkingSetSizeEx(NativeMethods.GetCurrentProcess(), -1, -1, 0);
    }
}
