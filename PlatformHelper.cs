
using System.Runtime.InteropServices;

public static class PlatformHelper
{
  public static string GetHost()
  {
    string os = 
      OperatingSystem.IsWindows() ? "win" :
      OperatingSystem.IsFreeBSD() ? "freebsd" :
      OperatingSystem.IsLinux() ? "linux" :
      OperatingSystem.IsMacOS() ? "macos" :
      throw new PlatformNotSupportedException();

    string arch = RuntimeInformation.ProcessArchitecture switch
    {
      Architecture.X86 => "x86",
      Architecture.X64 => "x64",
      Architecture.Arm => "arm",
      Architecture.Arm64 => "arm64",
      Architecture.RiscV64 => "rv64",
      _ => throw new PlatformNotSupportedException()
    };
    return $"{os}_{arch}";
  }
}
