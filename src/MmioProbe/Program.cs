using System.Reflection;
using System.Runtime.Loader;

const string fanControlDirectory = @"C:\Program Files (x86)\FanControl";
const string libraryPath = fanControlDirectory + @"\LibreHardwareMonitorLib.dll";

AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string dependency = Path.Combine(fanControlDirectory, name.Name + ".dll");
    return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
};

Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(libraryPath);
Console.WriteLine(assembly.FullName);

foreach (Type type in assembly.GetTypes()
             .Where(t => t.FullName?.Contains("Ring0", StringComparison.OrdinalIgnoreCase) == true ||
                         t.FullName?.Contains("Pawn", StringComparison.OrdinalIgnoreCase) == true ||
                         t.FullName?.Contains("KernelDriver", StringComparison.OrdinalIgnoreCase) == true)
             .OrderBy(t => t.FullName))
{
    Console.WriteLine($"\nTYPE {type.FullName} public={type.IsPublic}");

    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                   BindingFlags.Static | BindingFlags.Instance |
                                                   BindingFlags.DeclaredOnly)
                                      .OrderBy(m => m.Name))
    {
        string parameters = string.Join(", ", method.GetParameters()
            .Select(p => $"{p.ParameterType} {p.Name}"));
        Console.WriteLine($"  {method.Attributes}: {method.ReturnType} {method.Name}({parameters})");
    }
}

Console.WriteLine("\nRESOURCES");
foreach (string resource in assembly.GetManifestResourceNames().Order())
    Console.WriteLine("  " + resource);

const string testResource = "LibreHardwareMonitor.Resources.PawnIo.IntelMSR.bin";
string outputDirectory = Path.Combine(AppContext.BaseDirectory, "modules");
Directory.CreateDirectory(outputDirectory);

using (Stream source = assembly.GetManifestResourceStream(testResource)
       ?? throw new InvalidOperationException($"Missing resource {testResource}"))
using (FileStream target = File.Create(Path.Combine(outputDirectory, "IntelMSR-signed.bin")))
    source.CopyTo(target);

byte[] signedBlob = File.ReadAllBytes(Path.Combine(outputDirectory, "IntelMSR-signed.bin"));
uint signatureLength = BitConverter.ToUInt32(signedBlob, 0);
int moduleOffset = checked(4 + (int)signatureLength);
File.WriteAllBytes(Path.Combine(outputDirectory, "IntelMSR-unsigned.amx"), signedBlob[moduleOffset..]);

Console.WriteLine($"\nEXTRACTED signed={signedBlob.Length} signature={signatureLength} " +
                  $"unsigned={signedBlob.Length - moduleOffset}");
