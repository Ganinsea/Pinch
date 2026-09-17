using System.Reflection;
[assembly: AssemblyTitle("Pinch")]
[assembly: AssemblyProduct("Pinch")]
[assembly: AssemblyCompany("Han")]
[assembly: AssemblyCopyright("Han")]
[assembly: AssemblyVersion(ImageCompressorFloat.AppInfo.AssemblyVersion)]
[assembly: AssemblyFileVersion(ImageCompressorFloat.AppInfo.AssemblyVersion)]
[assembly: AssemblyInformationalVersion(ImageCompressorFloat.AppInfo.Version)]
namespace ImageCompressorFloat
{
    internal static class AppInfo
    {
        public const string Version = "2.3.0";
        public const string AssemblyVersion = Version + ".0";
        public const long TargetBytes = 2000000;
    }
}
