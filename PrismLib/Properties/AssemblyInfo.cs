using System.Reflection;
using System.Runtime.InteropServices;

// Keep in step with Prism.Version — the bootstrapper picks a copy by ASSEMBLY version, read from
// the file's metadata without loading it, so a drifting AssemblyVersion silently breaks updates.
[assembly: AssemblyTitle("PrismLib")]
[assembly: AssemblyDescription("Shared arbitration layer for QuartzTeam's ADOFAI mods")]
[assembly: AssemblyCompany("QuartzTeam")]
[assembly: AssemblyProduct("PrismLib")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
