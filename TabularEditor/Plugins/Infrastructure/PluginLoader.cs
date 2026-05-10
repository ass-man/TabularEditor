using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TabularEditor.TOMWrapper;

namespace TabularEditor.Plugins.Infrastructure
{
    public class PluginLoader
    {
        public IRuntimeWindowPlugin Load(PluginDescriptor descriptor, string pluginsRoot)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            var type = descriptor.SourceType == PluginSourceType.Script
                ? LoadScriptPluginType(descriptor, pluginsRoot)
                : LoadAssemblyPluginType(descriptor, pluginsRoot);

            if (!typeof(IRuntimeWindowPlugin).IsAssignableFrom(type))
                throw new InvalidOperationException($"Plugin type '{type.FullName}' does not implement {nameof(IRuntimeWindowPlugin)}.");

            var instance = Activator.CreateInstance(type) as IRuntimeWindowPlugin;
            if (instance == null)
                throw new InvalidOperationException($"Could not create plugin instance for type '{type.FullName}'.");

            return instance;
        }

        private static Type LoadAssemblyPluginType(PluginDescriptor descriptor, string pluginsRoot)
        {
            Assembly assembly;
            if (string.IsNullOrWhiteSpace(descriptor.Source) || descriptor.Source.Equals("self", StringComparison.InvariantCultureIgnoreCase))
            {
                assembly = Assembly.GetExecutingAssembly();
            }
            else
            {
                var sourcePath = ResolveSourcePath(descriptor.Source, pluginsRoot);
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException($"Plugin assembly not found: {sourcePath}", sourcePath);

                assembly = Assembly.LoadFrom(sourcePath);
            }

            return ResolvePluginType(descriptor, assembly);
        }

        private static Type LoadScriptPluginType(PluginDescriptor descriptor, string pluginsRoot)
        {
            var sourcePath = ResolveSourcePath(descriptor.Source, pluginsRoot);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Plugin script not found: {sourcePath}", sourcePath);

            var scriptDirectory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(scriptDirectory) || !Directory.Exists(scriptDirectory))
                throw new DirectoryNotFoundException($"Plugin script directory not found: {scriptDirectory}");

            var compilerParameters = new CompilerParameters
            {
                GenerateExecutable = false,
                GenerateInMemory = true,
                IncludeDebugInformation = false
            };
            compilerParameters.ReferencedAssemblies.Add("System.dll");
            compilerParameters.ReferencedAssemblies.Add("System.Core.dll");
            compilerParameters.ReferencedAssemblies.Add("System.Drawing.dll");
            compilerParameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            AddReferenceIfResolvable(compilerParameters, Assembly.GetExecutingAssembly().Location);
            AddReferenceIfResolvable(compilerParameters, Assembly.GetAssembly(typeof(TabularModelHandler))?.Location);
            AddReferenceIfResolvable(compilerParameters, Assembly.GetAssembly(typeof(PluginHostContext))?.Location);

            // Best-effort references for common runtime dependencies:
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var configuration = new DirectoryInfo(baseDir.TrimEnd(Path.DirectorySeparatorChar)).Name;

            AddReferenceIfResolvable(
                compilerParameters,
                Path.Combine(baseDir, "TabularEditor.exe"));

            AddReferenceIfResolvable(
                compilerParameters,
                Path.GetFullPath(Path.Combine(
                    baseDir,
                    @"..\..\..\TOMWrapper\bin\" + configuration + @"\TOMWrapper.dll")));

            var prelude = string.Join(Environment.NewLine, new[]
            {
                "using System;",
                "using System.Collections;",
                "using System.Collections.Generic;",
                "using System.IO;",
                "using System.Linq;",
                "using System.Text;",
                "using System.Drawing;",
                "using System.Windows.Forms;",
                "using TabularEditor;",
                "using TabularEditor.UI;",
                "using TabularEditor.Plugins.Infrastructure;"
            });

            var additionalUsings = new List<string>();
            var sourceFiles = GetScriptSourcesForFolder(scriptDirectory, sourcePath, additionalUsings);
            var allUsings = prelude
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Concat(additionalUsings)
                .Distinct(StringComparer.InvariantCultureIgnoreCase);

            var combinedSource = string.Join(Environment.NewLine, allUsings) +
                                 Environment.NewLine + Environment.NewLine +
                                 string.Join(Environment.NewLine + Environment.NewLine, sourceFiles);

            CompilerResults results;
            using (var provider = new CSharpCodeProvider())
            {
                results = provider.CompileAssemblyFromSource(compilerParameters, combinedSource);
            }

            if (results.Errors.HasErrors)
            {
                var message = string.Join(Environment.NewLine, results.Errors.Cast<CompilerError>()
                    .Where(e => !e.IsWarning)
                    .Select(e => $"({e.Line},{e.Column}) {e.ErrorNumber}: {e.ErrorText}"));
                throw new InvalidOperationException($"Script plugin compilation failed for '{sourcePath}'.{Environment.NewLine}{message}");
            }

            return ResolvePluginType(descriptor, results.CompiledAssembly);
        }

        private static IEnumerable<string> GetScriptSourcesForFolder(string scriptDirectory, string primaryScriptPath, List<string> additionalUsings)
        {
            var allScripts = Directory
                .EnumerateFiles(scriptDirectory, "*.csx", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.InvariantCultureIgnoreCase)
                .ToList();

            if (!allScripts.Any(p => p.Equals(primaryScriptPath, StringComparison.InvariantCultureIgnoreCase)))
                allScripts.Insert(0, primaryScriptPath);

            var orderedScripts = allScripts
                .OrderBy(p => p.Equals(primaryScriptPath, StringComparison.InvariantCultureIgnoreCase) ? 0 : 1)
                .ThenBy(Path.GetFileName, StringComparer.InvariantCultureIgnoreCase)
                .ToList();

            foreach (var scriptPath in orderedScripts)
            {
                var scriptCode = File.ReadAllText(scriptPath);
                yield return NormalizeScriptCode(scriptCode, additionalUsings);
            }
        }

        private static string NormalizeScriptCode(string scriptCode, List<string> additionalUsings)
        {
            if (string.IsNullOrWhiteSpace(scriptCode)) return scriptCode;

            var lines = scriptCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var filtered = lines.Where(line =>
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("#load ", StringComparison.InvariantCultureIgnoreCase))
                    return false;

                if (IsUsingDirective(trimmed))
                {
                    additionalUsings.Add(trimmed);
                    return false;
                }

                return true;
            });

            return string.Join(Environment.NewLine, filtered);
        }

        private static bool IsUsingDirective(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            if (!line.StartsWith("using ", StringComparison.InvariantCulture)) return false;
            if (!line.EndsWith(";", StringComparison.InvariantCulture)) return false;
            if (line.StartsWith("using static ", StringComparison.InvariantCulture)) return true;
            if (line.Contains("=")) return true;
            return true;
        }

        private static void AddReferenceIfResolvable(CompilerParameters parameters, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!File.Exists(path)) return;
            if (HasEquivalentReference(parameters, path)) return;
            parameters.ReferencedAssemblies.Add(path);
        }

        private static bool HasEquivalentReference(CompilerParameters parameters, string candidatePath)
        {
            var candidateSimpleName = GetAssemblySimpleName(candidatePath);
            foreach (string existingReference in parameters.ReferencedAssemblies)
            {
                if (existingReference.Equals(candidatePath, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                var existingSimpleName = GetAssemblySimpleName(existingReference);
                if (!string.IsNullOrWhiteSpace(existingSimpleName) &&
                    existingSimpleName.Equals(candidateSimpleName, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string GetAssemblySimpleName(string assemblyPathOrName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPathOrName)) return null;

            if (File.Exists(assemblyPathOrName))
            {
                try
                {
                    return AssemblyName.GetAssemblyName(assemblyPathOrName).Name;
                }
                catch
                {
                    return Path.GetFileNameWithoutExtension(assemblyPathOrName);
                }
            }

            return Path.GetFileNameWithoutExtension(assemblyPathOrName);
        }

        private static Type ResolvePluginType(PluginDescriptor descriptor, Assembly assembly)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.EntryType))
            {
                var namedType = assembly.GetType(descriptor.EntryType, false, true);
                if (namedType != null) return namedType;
            }

            var autoType = assembly.GetTypes().FirstOrDefault(t =>
                !t.IsAbstract &&
                typeof(IRuntimeWindowPlugin).IsAssignableFrom(t));

            if (autoType == null)
                throw new InvalidOperationException($"Could not find a plugin type in '{assembly.FullName}'.");

            return autoType;
        }

        private static string ResolveSourcePath(string source, string pluginsRoot)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException("Plugin source path is required.");

            if (Path.IsPathRooted(source)) return source;
            return Path.GetFullPath(Path.Combine(pluginsRoot, source));
        }
    }
}
