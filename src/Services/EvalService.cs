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

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                            parms.ReferencedAssemblies.Add(asm.Location);
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
                        try { parms.ReferencedAssemblies.Add(dll); }
                        catch { }
                    }
                }

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
