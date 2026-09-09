
using System.Reflection;
using Tomlyn;
using Tomlyn.Model;

public static class ResourceHelper
{
  public static string ExtractClangFormat()
  {
    string outputDir = "clang-format";

    Directory.CreateDirectory(outputDir);

    string outputPath = Path.Combine(outputDir, "clang-format.exe");

    if (File.Exists(outputPath))
      return outputPath;

    Assembly assembly = Assembly.GetExecutingAssembly();

    string resourceName = "Singular.clang_format.clang-format.exe";

    using Stream? resource = assembly.GetManifestResourceStream(resourceName) ?? throw new Exception($"Could not find embedded resource {resourceName}");
    using FileStream file = File.Create(outputPath);
    resource.CopyTo(file);
    return outputPath;
  }

  public static TomlTable ExtractToml(string name)
  {
    Assembly assembly = Assembly.GetExecutingAssembly();
    string resourceName = $"Singular.data.{name}.toml";
    using Stream resource = assembly.GetManifestResourceStream(resourceName) ?? throw new Exception($"Could not find embedded resource {resourceName}");
    TomlTable model = TomlSerializer.Deserialize<TomlTable>(resource)!;
    return model;
  }

  public static string ExtractSgl(string name)
  {
    Assembly assembly = Assembly.GetExecutingAssembly();
    string resourceName = $"Singular.data.{name}.sgl";
    using Stream resource = assembly.GetManifestResourceStream(resourceName) ?? throw new Exception($"Could not find embedded resource {resourceName}");
    using StreamReader reader = new(resource);
    return reader.ReadToEnd();
  }

  public static (string path, string content) ExtractSglWithPath(string name) => (name, ExtractSgl(name));
}
