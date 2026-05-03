using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
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

            var scriptCode = File.ReadAllText(sourcePath);

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
            AddReferenceIfResolvable(compilerParameters, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TOMWrapper.dll"));
            AddReferenceIfResolvable(compilerParameters, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TabularEditor.exe"));

            var source = string.Join(Environment.NewLine, new[]
            {
                "using System;",
                "using System.Linq;",
                "using System.Drawing;",
                "using System.Windows.Forms;",
                "using TabularEditor;",
                "using TabularEditor.UI;",
                "using TabularEditor.Plugins.Infrastructure;",
                scriptCode
            });

            CompilerResults results;
            using (var provider = new CSharpCodeProvider())
            {
                results = provider.CompileAssemblyFromSource(compilerParameters, source);
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

        private static void AddReferenceIfResolvable(CompilerParameters parameters, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!File.Exists(path)) return;
            if (parameters.ReferencedAssemblies.Contains(path)) return;
            parameters.ReferencedAssemblies.Add(path);
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
