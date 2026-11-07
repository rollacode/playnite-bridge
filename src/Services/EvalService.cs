namespace PlayniteBridge.Services
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.CSharp;
    using Playnite.SDK;

    internal class EvalErrorInfo
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string ErrorNumber { get; set; }
        public string Message { get; set; }
    }

    internal class EvalResult
    {
        public bool Success { get; set; }
        public object Result { get; set; }
        public string ResultType { get; set; }
        public string Error { get; set; }
        public string ExceptionType { get; set; }
        public List<string> CompilationErrors { get; set; }
        public List<EvalErrorInfo> CompilationErrorDetails { get; set; }
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// Describes one candidate compiler reference. References are compared by the
    /// assembly simple name rather than by file path, because the same assembly can be
    /// loaded from several locations (app base + extension folder).
    /// </summary>
    internal sealed class ReferenceCandidate
    {
        public string SimpleName { get; set; }
        public string Identity { get; set; }
        public string Location { get; set; }
        public Version Version { get; set; }
        public bool IsInBaseDirectory { get; set; }

        public bool IsPreferredOver(ReferenceCandidate other)
        {
            if (other == null) return true;
            if (IsInBaseDirectory != other.IsInBaseDirectory) return IsInBaseDirectory;
            return (Version ?? new Version(0, 0)) > (other.Version ?? new Version(0, 0));
        }
    }

    internal class EvalService
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        // Only Microsoft framework assemblies are resolved from the GAC. Plugin-local
        // copies must never be added, they are either already referenced or private.
        private static readonly HashSet<string> FrameworkPublicKeyTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "b77a5c561934e089", // ECMA
                "b03f5f7f11d50a3a", // Microsoft framework
                "31bf3856ad364e35", // Windows
            };

        private static readonly string[] DuplicateReferenceErrorNumbers = { "CS1703", "CS1704" };

        private static readonly Regex QuotedAssemblyName = new Regex(@"'([^,']+)", RegexOptions.Compiled);

        private static readonly object GacCacheLock = new object();
        private static readonly Dictionary<string, string> GacCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string GenerateSource(string userCode, bool isExpression)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Diagnostics;");
            sb.AppendLine("using System.Globalization;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine("using System.Text.RegularExpressions;");
            sb.AppendLine("using System.Threading.Tasks;");
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

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                foreach (var path in BuildReferenceList(assemblies, baseDirectory))
                    parms.ReferencedAssemblies.Add(path);

                // Add facade assemblies that may not be loaded yet
                AddFacadeReferences(parms);

                // Fill in framework assemblies that are only reachable through the GAC
                AddFrameworkReferences(parms, CollectDependencies(assemblies), ReferencedSimpleNames(parms));

                var compiled = provider.CompileAssemblyFromSource(parms, source);

                // Last resort: csc can still unify two references of the same simple name
                // when the framework versions differ. Drop the loser and retry once.
                if (HasDuplicateReferenceErrors(compiled) &&
                    DropConflictingReferences(parms, compiled, baseDirectory))
                {
                    Logger.Warn("Eval compilation hit CS1703/CS1704; retrying with conflicting references removed");
                    compiled = provider.CompileAssemblyFromSource(parms, source);
                }

                if (compiled.Errors.HasErrors)
                {
                    var errors = new List<string>();
                    var details = new List<EvalErrorInfo>();
                    foreach (CompilerError err in compiled.Errors)
                    {
                        if (err.IsWarning) continue;

                        errors.Add($"({err.Line},{err.Column}): {err.ErrorNumber} {err.ErrorText}");
                        details.Add(new EvalErrorInfo
                        {
                            Line = err.Line,
                            Column = err.Column,
                            ErrorNumber = err.ErrorNumber,
                            Message = err.ErrorText,
                        });
                    }

                    return new EvalResult
                    {
                        Success = false,
                        Error = "Compilation failed",
                        CompilationErrors = errors,
                        CompilationErrorDetails = details,
                    };
                }

                return new EvalResult
                {
                    Success = true,
                    Result = compiled.CompiledAssembly.GetType("ScriptHost")
                        .GetMethod("Execute", BindingFlags.Public | BindingFlags.Static)
                };
            }
        }

        /// <summary>
        /// Builds the reference list from the loaded assemblies, keeping exactly one file
        /// per assembly simple name. De-duplicating by path is not enough: the same
        /// identity can be loaded from two locations (Assembly.LoadFrom puts extension
        /// assemblies in a separate load context), which makes csc fail with CS1703, and
        /// extensions that bundle their own copy of a shared assembly introduce a second
        /// version of the same simple name, which fails with CS1704.
        /// </summary>
        internal static string[] BuildReferenceList(IEnumerable<Assembly> assemblies, string baseDirectory)
        {
            var candidates = new List<ReferenceCandidate>();
            foreach (var asm in assemblies ?? Enumerable.Empty<Assembly>())
            {
                var candidate = TryDescribe(asm, baseDirectory);
                if (candidate != null) candidates.Add(candidate);
            }
            return SelectDistinctReferences(candidates);
        }

        internal static string[] SelectDistinctReferences(IEnumerable<ReferenceCandidate> candidates)
        {
            var selected = new Dictionary<string, ReferenceCandidate>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates ?? Enumerable.Empty<ReferenceCandidate>())
            {
                if (candidate == null || string.IsNullOrEmpty(candidate.SimpleName)) continue;
                if (string.IsNullOrEmpty(candidate.Location)) continue;
                if (!paths.Add(candidate.Location)) continue;

                ReferenceCandidate existing;
                if (!selected.TryGetValue(candidate.SimpleName, out existing))
                {
                    selected[candidate.SimpleName] = candidate;
                    continue;
                }

                var keep = candidate.IsPreferredOver(existing) ? candidate : existing;
                var drop = ReferenceEquals(keep, candidate) ? existing : candidate;
                selected[candidate.SimpleName] = keep;

                Logger.Warn($"[Eval] Dropping duplicate reference {keep.SimpleName}: {drop.Location} " +
                            $"(identity {drop.Identity}) in favour of {keep.Location}");
            }

            return selected.Values
                .OrderBy(c => c.SimpleName, StringComparer.OrdinalIgnoreCase)
                .Select(c => c.Location)
                .ToArray();
        }

        private static ReferenceCandidate TryDescribe(Assembly asm, string baseDirectory)
        {
            if (asm == null) return null;

            string location;
            AssemblyName name;
            try
            {
                if (asm.IsDynamic) return null;
                location = asm.Location;
                name = asm.GetName();
            }
            catch (NotSupportedException) { return null; }
            catch (Exception) { return null; }

            if (string.IsNullOrEmpty(location) || !File.Exists(location)) return null;
            if (name == null || string.IsNullOrEmpty(name.Name)) return null;

            return new ReferenceCandidate
            {
                SimpleName = name.Name,
                Identity = name.FullName,
                Location = location,
                Version = name.Version,
                IsInBaseDirectory = IsInDirectory(location, baseDirectory),
            };
        }

        /// <summary>
        /// True when <paramref name="path"/> sits directly inside <paramref name="directory"/>.
        /// Extension folders live under the app folder, so a prefix check would treat them
        /// as the app's own copy; only an exact parent match counts here.
        /// </summary>
        internal static bool IsInDirectory(string path, string directory)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory)) return false;

            try
            {
                var parent = Path.GetDirectoryName(Path.GetFullPath(path));
                if (string.IsNullOrEmpty(parent)) return false;

                var target = Path.GetFullPath(directory);
                return string.Equals(
                    parent.TrimEnd(Path.DirectorySeparatorChar),
                    target.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        internal static bool IsUnderDirectory(string path, string directory)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory)) return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                var fullDirectory = Path.GetFullPath(directory);
                if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    fullDirectory += Path.DirectorySeparatorChar;
                return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static IEnumerable<AssemblyName> CollectDependencies(IEnumerable<Assembly> assemblies)
        {
            var dependencies = new List<AssemblyName>();
            foreach (var asm in assemblies ?? Enumerable.Empty<Assembly>())
            {
                try
                {
                    if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                    dependencies.AddRange(asm.GetReferencedAssemblies());
                }
                catch { }
            }
            return dependencies;
        }

        private static HashSet<string> ReferencedSimpleNames(CompilerParameters parms)
        {
            // CodeDOM references mscorlib implicitly, so it must count as present.
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mscorlib" };

            foreach (string path in parms.ReferencedAssemblies)
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(path);
                    if (!string.IsNullOrEmpty(name.Name)) names.Add(name.Name);
                }
                catch { }
            }
            return names;
        }

        private static void AddFacadeReferences(CompilerParameters parms)
        {
            string[] dlls;
            try
            {
                var facadeDir = Path.Combine(
                    System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                    "Facades");
                if (!Directory.Exists(facadeDir)) return;
                dlls = Directory.GetFiles(facadeDir, "*.dll");
            }
            catch { return; }

            var referenced = ReferencedSimpleNames(parms);
            var paths = new HashSet<string>(parms.ReferencedAssemblies.Cast<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var dll in dlls)
            {
                try
                {
                    if (!paths.Add(dll)) continue;

                    var name = AssemblyName.GetAssemblyName(dll);
                    if (name == null || string.IsNullOrEmpty(name.Name)) continue;
                    if (!referenced.Add(name.Name)) continue;

                    parms.ReferencedAssemblies.Add(dll);
                }
                catch { }
            }
        }

        /// <summary>
        /// Resolves framework assemblies that are reachable only through the GAC (the
        /// runtime has no Facades directory when Windows metadata is loaded). Only
        /// Microsoft-token assemblies are considered, dependencies are walked
        /// transitively through metadata, and nothing is ever loaded into the execution
        /// context or probed from extension folders.
        /// </summary>
        internal static void AddFrameworkReferences(CompilerParameters parms, IEnumerable<AssemblyName> dependencies,
            HashSet<string> referencedNames)
        {
            if (parms == null) return;

            if (referencedNames == null) referencedNames = ReferencedSimpleNames(parms);

            var pending = new Queue<AssemblyName>(dependencies ?? Enumerable.Empty<AssemblyName>());
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(parms.ReferencedAssemblies.Cast<string>(), StringComparer.OrdinalIgnoreCase);

            while (pending.Count > 0)
            {
                var name = pending.Dequeue();
                if (name == null || string.IsNullOrEmpty(name.Name)) continue;
                if (!string.IsNullOrEmpty(name.FullName) && !visited.Add(name.FullName)) continue;

                // Preserve the reference the application already resolved. csc unifies
                // older framework references to the target framework, and a second copy
                // of the same simple name is what produces CS1703/CS1704.
                if (referencedNames.Contains(name.Name)) continue;

                string location;
                if (!TryResolveFrameworkPath(name, out location)) continue;
                if (!paths.Add(location)) continue;

                referencedNames.Add(name.Name);
                parms.ReferencedAssemblies.Add(location);

                try
                {
                    var resolved = Assembly.ReflectionOnlyLoadFrom(location);
                    foreach (var dependency in resolved.GetReferencedAssemblies()) pending.Enqueue(dependency);
                }
                catch { }
            }
        }

        private static bool TryResolveFrameworkPath(AssemblyName name, out string location)
        {
            location = null;

            byte[] token;
            try { token = name.GetPublicKeyToken(); }
            catch { return false; }

            if (token == null || token.Length == 0) return false;

            var tokenText = BitConverter.ToString(token).Replace("-", string.Empty).ToLowerInvariant();
            if (!FrameworkPublicKeyTokens.Contains(tokenText)) return false;

            var key = name.FullName ?? name.Name;

            lock (GacCacheLock)
            {
                string cached;
                if (GacCache.TryGetValue(key, out cached))
                {
                    location = cached;
                    return !string.IsNullOrEmpty(cached);
                }
            }

            string resolvedPath = null;
            try
            {
                // Reflection-only binding keeps the load out of the execution context.
                var loaded = Assembly.ReflectionOnlyLoad(key);
                var candidate = loaded == null ? null : loaded.Location;
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate) && IsInGac(candidate))
                    resolvedPath = candidate;
            }
            catch { }

            lock (GacCacheLock) { GacCache[key] = resolvedPath; }

            location = resolvedPath;
            return resolvedPath != null;
        }

        private static bool IsInGac(string path)
        {
            try
            {
                var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (string.IsNullOrEmpty(windows)) return false;
                return IsUnderDirectory(path, Path.Combine(windows, "Microsoft.NET", "assembly"));
            }
            catch { return false; }
        }

        internal static bool HasDuplicateReferenceErrors(CompilerResults compiled)
        {
            if (compiled == null) return false;

            foreach (CompilerError err in compiled.Errors)
            {
                if (err.IsWarning) continue;
                if (Array.IndexOf(DuplicateReferenceErrorNumbers, err.ErrorNumber) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Reduces every reference set named by a CS1703/CS1704 diagnostic down to a
        /// single file, preferring the app base copy. Returns true when the reference
        /// list actually changed, so the caller can retry exactly once.
        /// </summary>
        internal static bool DropConflictingReferences(CompilerParameters parms, CompilerResults compiled, string baseDirectory)
        {
            if (parms == null || compiled == null) return false;

            var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CompilerError err in compiled.Errors)
            {
                if (err.IsWarning) continue;
                if (Array.IndexOf(DuplicateReferenceErrorNumbers, err.ErrorNumber) < 0) continue;

                var name = ExtractConflictingSimpleName(err.ErrorText);
                if (!string.IsNullOrEmpty(name)) conflicts.Add(name);
            }

            if (conflicts.Count == 0) return false;

            var kept = new Dictionary<string, ReferenceCandidate>(StringComparer.OrdinalIgnoreCase);
            var untouched = new List<string>();
            var dropped = 0;

            foreach (string path in parms.ReferencedAssemblies)
            {
                AssemblyName name = null;
                try { name = AssemblyName.GetAssemblyName(path); }
                catch { }

                if (name == null || string.IsNullOrEmpty(name.Name) || !conflicts.Contains(name.Name))
                {
                    untouched.Add(path);
                    continue;
                }

                var candidate = new ReferenceCandidate
                {
                    SimpleName = name.Name,
                    Identity = name.FullName,
                    Location = path,
                    Version = name.Version,
                    IsInBaseDirectory = IsInDirectory(path, baseDirectory),
                };

                ReferenceCandidate existing;
                if (!kept.TryGetValue(name.Name, out existing))
                {
                    kept[name.Name] = candidate;
                    continue;
                }

                if (candidate.IsPreferredOver(existing)) kept[name.Name] = candidate;
                dropped++;
            }

            if (dropped == 0) return false;

            parms.ReferencedAssemblies.Clear();
            parms.ReferencedAssemblies.AddRange(untouched.ToArray());
            foreach (var candidate in kept.Values) parms.ReferencedAssemblies.Add(candidate.Location);
            return true;
        }

        internal static string ExtractConflictingSimpleName(string errorText)
        {
            if (string.IsNullOrEmpty(errorText)) return null;

            var match = QuotedAssemblyName.Match(errorText);
            return match.Success ? match.Groups[1].Value.Trim() : null;
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
