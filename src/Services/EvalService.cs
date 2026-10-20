namespace PlayniteBridge.Services
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CSharp;

    internal class EvalResult
    {
        public bool Success { get; set; }
        public object Result { get; set; }
        public string ResultType { get; set; }
        public string Error { get; set; }
        public string ExceptionType { get; set; }
        public List<string> CompilationErrors { get; set; }
        public long DurationMs { get; set; }
    }

    internal class EvalService
    {
        public string GenerateSource(string userCode, bool isExpression)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using Playnite.SDK;");
            sb.AppendLine("using Playnite.SDK.Models;");
            sb.AppendLine("using Playnite.SDK.Plugins;");
            sb.AppendLine("public class ScriptHost {");
            sb.AppendLine("  public static object Execute(IPlayniteAPI PlayniteApi, GenericPlugin Plugin) {");
            if (isExpression)
                sb.AppendLine("    return (" + userCode + ");");
            else
            {
                sb.AppendLine("    " + userCode);
                sb.AppendLine("    return null;");
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public bool IsExpression(string code) => !code.Contains(";");

        public EvalResult Compile(string source)
        {
            using (var provider = new CSharpCodeProvider())
            {
                var parms = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false,
                    TreatWarningsAsErrors = false,
                };
                var frameworkDependencies = new List<AssemblyName>();
                var referenceIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                        {
                            if (referenceIdentities.Add(asm.FullName)) parms.ReferencedAssemblies.Add(asm.Location);
                            frameworkDependencies.AddRange(asm.GetReferencedAssemblies());
                        }
                    }
                    catch { }
                }

                // Add facade assemblies that may not be loaded yet
                var facadeDir = System.IO.Path.Combine(
                    System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                    "Facades");
                if (System.IO.Directory.Exists(facadeDir))
                {
                    foreach (var dll in System.IO.Directory.GetFiles(facadeDir, "*.dll"))
                    {
                        try
                        {
                            if (referenceIdentities.Add(AssemblyName.GetAssemblyName(dll).FullName))
                                parms.ReferencedAssemblies.Add(dll);
                        }
                        catch { }
                    }
                }

                AddFrameworkReferences(parms, frameworkDependencies, referenceIdentities);

                var compiled = provider.CompileAssemblyFromSource(parms, source);

                if (compiled.Errors.HasErrors)
                {
                    var errors = new List<string>();
                    foreach (CompilerError err in compiled.Errors)
                        if (!err.IsWarning)
                            errors.Add($"({err.Line},{err.Column}): {err.ErrorNumber} {err.ErrorText}");

                    return new EvalResult { Success = false, Error = "Compilation failed", CompilationErrors = errors };
                }

                return new EvalResult
                {
                    Success = true,
                    Result = compiled.CompiledAssembly.GetType("ScriptHost")
                        .GetMethod("Execute", BindingFlags.Public | BindingFlags.Static)
                };
            }
        }

        // Framework facades can live in the GAC rather than a runtime Facades
        // directory. Resolve the dependency graph, including transitive references,
        // without loading code into the execution context or probing plugin folders.
        internal static void AddFrameworkReferences(CompilerParameters parms, IEnumerable<AssemblyName> dependencies,
            HashSet<string> referenceIdentities)
        {
            var pending = new Queue<AssemblyName>(dependencies);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var referencedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var identity in referenceIdentities) referencedNames.Add(new AssemblyName(identity).Name);
            foreach (string path in parms.ReferencedAssemblies) paths.Add(path);
            while (pending.Count != 0)
            {
                var name = pending.Dequeue();
                if (!visited.Add(name.FullName)) continue;
                // Preserve the executing application's selected version. The C#
                // compiler also unifies older framework references to the current
                // framework; adding their GAC copies can produce CS1703 even when
                // the file paths and full assembly identities differ.
                if (referencedNames.Contains(name.Name)) continue;
                var token = BitConverter.ToString(name.GetPublicKeyToken() ?? new byte[0]).Replace("-", "").ToLowerInvariant();
                if (token != "b77a5c561934e089" && token != "b03f5f7f11d50a3a" &&
                    token != "31bf3856ad364e35" && token != "cc7b13ffcd2ddd51") continue;
                try
                {
                    var assembly = Assembly.ReflectionOnlyLoad(name.FullName);
                    if (!assembly.GlobalAssemblyCache || string.IsNullOrEmpty(assembly.Location)) continue;
                    if (!referencedNames.Add(assembly.GetName().Name)) continue;
                    if (referenceIdentities.Add(assembly.FullName) && paths.Add(assembly.Location))
                        parms.ReferencedAssemblies.Add(assembly.Location);
                    foreach (var dependency in assembly.GetReferencedAssemblies()) pending.Enqueue(dependency);
                }
                catch (System.IO.FileNotFoundException) { }
                catch (System.IO.FileLoadException) { }
                catch (BadImageFormatException) { }
            }
        }

        public EvalResult Execute(MethodInfo method, object[] args, int timeoutMs, bool onUiThread, Action<Action> uiDispatcher)
        {
            var sw = Stopwatch.StartNew();
            object execResult = null;
            Exception execError = null;

            Action invoke = () =>
            {
                try { execResult = method.Invoke(null, args); }
                catch (TargetInvocationException tie) { execError = tie.InnerException ?? tie; }
                catch (Exception ex) { execError = ex; }
            };

            if (onUiThread && uiDispatcher != null)
            {
                uiDispatcher(invoke);
            }
            else
            {
                var task = Task.Run(() => invoke());
                if (!task.Wait(timeoutMs))
                    return new EvalResult { Success = false, Error = $"Script timed out after {timeoutMs}ms", DurationMs = sw.ElapsedMilliseconds };
            }

            sw.Stop();

            if (execError != null)
                return new EvalResult { Success = false, Error = execError.Message, ExceptionType = execError.GetType().Name, DurationMs = sw.ElapsedMilliseconds };

            return new EvalResult
            {
                Success = true,
                Result = execResult,
                ResultType = execResult?.GetType()?.Name,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }
}
