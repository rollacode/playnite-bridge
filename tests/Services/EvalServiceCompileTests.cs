namespace PlayniteBridge.Tests.Services
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using PlayniteBridge.Services;

    /// <summary>
    /// Covers the reference repair around compilation: framework lookups that must not
    /// add a second copy, the one-shot CS1703/CS1704 retry, and the structured
    /// diagnostics returned to API clients.
    /// </summary>
    [TestFixture]
    public class EvalServiceCompileTests
    {
        // --- structured diagnostics ---

        [Test]
        public void Compile_InvalidCode_PopulatesStructuredErrorDetails()
        {
            var svc = new EvalService();
            var result = svc.Compile(svc.GenerateSource("this is not valid c#;;;", false));

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.CompilationErrorDetails);
            Assert.Greater(result.CompilationErrorDetails.Count, 0);

            var first = result.CompilationErrorDetails[0];
            StringAssert.StartsWith("CS", first.ErrorNumber);
            Assert.GreaterOrEqual(first.Line, 1);
            StringAssert.Contains(first.ErrorNumber, result.CompilationErrors[0]);
            Assert.IsNotNull(first.Message);
        }

        [Test]
        public void Compile_ValidCode_StillSucceedsWithDeduplicatedReferences()
        {
            var svc = new EvalService();
            var result = svc.Compile(svc.GenerateSource("\"a\" + \"b\"", true));

            Assert.IsTrue(result.Success, "Reference de-duplication must not break a plain compile.");
            Assert.IsInstanceOf<MethodInfo>(result.Result);
        }

        // --- framework reference resolution ---

        [Test]
        public void AddFrameworkReferences_PluginAndMissingAssemblies_AreNotAdded()
        {
            var parms = Parameters();

            EvalService.AddFrameworkReferences(parms, new[]
            {
                typeof(EvalService).Assembly.GetName(),
                new AssemblyName("MissingFrameworkFacade, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
            }, Identities("mscorlib"));

            Assert.IsEmpty(parms.ReferencedAssemblies,
                "Unsigned plugin assemblies and unresolvable facades must never be referenced.");
        }

        [Test]
        public void AddFrameworkReferences_ExistingNameIsNotDuplicated()
        {
            var existing = typeof(Enumerable).Assembly; // System.Core
            var shadowCopy = ShadowCopyOf(existing);
            var temporaryDirectory = Path.GetDirectoryName(shadowCopy);

            try
            {
                var parms = Parameters(shadowCopy);
                var older = new AssemblyName("System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

                EvalService.AddFrameworkReferences(parms, new[] { older }, Identities(existing.GetName().Name, "mscorlib"));

                CollectionAssert.AreEqual(
                    new[] { shadowCopy },
                    parms.ReferencedAssemblies.Cast<string>().ToArray(),
                    "A GAC copy or older framework version must not duplicate the application's reference.");
            }
            finally { Directory.Delete(temporaryDirectory, true); }
        }

        [Test]
        public void AddFrameworkReferences_IsIdempotent()
        {
            var parms = Parameters();
            var identities = Identities("mscorlib");
            var dependencies = new[]
            {
                new AssemblyName("System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
            };

            EvalService.AddFrameworkReferences(parms, dependencies, identities);
            var count = parms.ReferencedAssemblies.Count;

            EvalService.AddFrameworkReferences(parms, dependencies, identities);

            Assert.AreEqual(count, parms.ReferencedAssemblies.Count);
        }

        // --- CS1703 / CS1704 retry ---

        [Test]
        public void ExtractConflictingSimpleName_ReadsBothDiagnosticShapes()
        {
            const string cs1703 = "An assembly with the same identity 'System.ValueTuple, Version=4.0.3.0, " +
                                  "Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51' has already been imported. " +
                                  "Try removing one of the duplicate references.";
            const string cs1704 = "An assembly with the same simple name 'System.Memory, Version=4.0.1.1, " +
                                  "Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51' has already been imported.";

            Assert.AreEqual("System.ValueTuple", EvalService.ExtractConflictingSimpleName(cs1703));
            Assert.AreEqual("System.Memory", EvalService.ExtractConflictingSimpleName(cs1704));
            Assert.IsNull(EvalService.ExtractConflictingSimpleName(null));
            Assert.IsNull(EvalService.ExtractConflictingSimpleName("no quoted name here"));
        }

        [Test]
        public void HasDuplicateReferenceErrors_OnlyMatchesCs1703AndCs1704()
        {
            Assert.IsTrue(EvalService.HasDuplicateReferenceErrors(Results("CS1703")), "CS1703 is the reported bug.");
            Assert.IsTrue(EvalService.HasDuplicateReferenceErrors(Results("CS1704")), "Extensions also trigger CS1704.");
            Assert.IsFalse(EvalService.HasDuplicateReferenceErrors(Results("CS1002")));
            Assert.IsFalse(EvalService.HasDuplicateReferenceErrors(null));
        }

        [Test]
        public void DropConflictingReferences_CollapsesDuplicateToSingleFile()
        {
            var existing = typeof(Enumerable).Assembly; // System.Core
            var copied = ShadowCopyOf(existing);
            var temporaryDirectory = Path.GetDirectoryName(copied);

            try
            {
                var second = Path.Combine(temporaryDirectory, "copy-" + Path.GetFileName(copied));
                File.Copy(copied, second);

                var parms = Parameters(copied, second);
                var changed = EvalService.DropConflictingReferences(parms, Results("CS1703", existing.GetName().Name), temporaryDirectory);

                Assert.IsTrue(changed, "The retry must only run when the list actually changed.");
                Assert.AreEqual(1, parms.ReferencedAssemblies.Count);
                Assert.AreEqual(existing.GetName().Name,
                    AssemblyName.GetAssemblyName((string)parms.ReferencedAssemblies[0]).Name);
            }
            finally { Directory.Delete(temporaryDirectory, true); }
        }

        [Test]
        public void DropConflictingReferences_NoDuplicateDiagnostic_LeavesListAlone()
        {
            var parms = Parameters(typeof(Enumerable).Assembly.Location);

            Assert.IsFalse(EvalService.DropConflictingReferences(parms, Results("CS1002"), Path.GetTempPath()));
            Assert.AreEqual(1, parms.ReferencedAssemblies.Count);
        }

        [Test]
        public void DropConflictingReferences_UnknownAssemblyName_LeavesListAlone()
        {
            var existing = typeof(Enumerable).Assembly; // System.Core
            var copied = ShadowCopyOf(existing);
            var temporaryDirectory = Path.GetDirectoryName(copied);

            try
            {
                var mscorlib = typeof(object).Assembly.Location;
                var parms = Parameters(copied, mscorlib);

                Assert.IsFalse(EvalService.DropConflictingReferences(parms, Results("CS1704", "System.Memory"), temporaryDirectory),
                    "Nothing is dropped when the diagnostic names an assembly that is not referenced.");

                CollectionAssert.Contains(parms.ReferencedAssemblies.Cast<string>().ToArray(), mscorlib,
                    "Unrelated references must survive the retry.");
            }
            finally { Directory.Delete(temporaryDirectory, true); }
        }

        private static CompilerResults Results(string errorNumber, string assemblyName)
        {
            var results = new CompilerResults(new TempFileCollection());
            results.Errors.Add(new CompilerError
            {
                ErrorNumber = errorNumber,
                ErrorText = string.IsNullOrEmpty(assemblyName)
                    ? "unrelated diagnostic"
                    : $"An assembly with the same identity '{assemblyName}, Version=4.0.0.0, Culture=neutral, " +
                      "PublicKeyToken=b77a5c561934e089' has already been imported. " +
                      "Try removing one of the duplicate references.",
            });
            return results;
        }

        private static CompilerResults Results(string errorNumber)
        {
            return Results(errorNumber, null);
        }


        // --- end-to-end: duplicate assembly in the AppDomain ---

        [Test]
        public void Compile_WithSecondCopyOfSameAssemblyLoaded_StillCompiles()
        {
            // Reproduces Playnite's extension layout: the same assembly identity is
            // present twice with different paths. Before the fix this failed every eval
            // call with CS1703 before any user code was parsed.
            var copy = ShadowCopyOf(typeof(EvalService).Assembly);
            var temporaryDirectory = Path.GetDirectoryName(copy);

            try
            {
                var second = Assembly.LoadFile(copy);
                Assert.AreNotEqual(typeof(EvalService).Assembly.Location, second.Location,
                    "The test needs a genuinely different path for the same identity.");

                var duplicateExists = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .GroupBy(a => a.GetName().FullName)
                    .Any(g => g.Count() > 1);
                Assert.IsTrue(duplicateExists, "The AppDomain must actually hold the duplicate.");

                var svc = new EvalService();
                var result = svc.Compile(svc.GenerateSource("\"ok\"", true));

                Assert.IsTrue(result.Success,
                    "A second copy of a loaded assembly must not break compilation. Errors: " +
                    string.Join(" | ", result.CompilationErrors ?? new List<string>()));
            }
            finally
            {
                try { Directory.Delete(temporaryDirectory, true); }
                catch (IOException) { }               // the CLR keeps a handle on the loaded file
                catch (UnauthorizedAccessException) { }
            }
        }

        // --- language surface (CodeDOM uses the in-box csc, i.e. C# 5) ---

        [Test]
        public void Compile_AsyncSnippet_Succeeds()
        {
            var svc = new EvalService();
            var source = svc.GenerateSource(
                "Func<Task<int>> f = async () => { await Task.Delay(1); return 5; }; return f().Result;", false);

            var result = svc.Compile(source);

            Assert.IsTrue(result.Success,
                "System.Threading.Tasks must be in the prelude. Errors: " +
                string.Join(" | ", result.CompilationErrors ?? new List<string>()));
        }

        [Test]
        public void Compile_StringInterpolation_IsRejectedByCSharp5Compiler()
        {
            // Documents the ceiling: eval compiles with the in-box csc (C# 5), so modern
            // syntax fails. If this ever starts passing, the compiler was upgraded and the
            // skill doc / docs/api.md limitations need revisiting.
            var svc = new EvalService();
            var result = svc.Compile(svc.GenerateSource("var n = 7; return $\"n={n}\";", false));

            Assert.IsFalse(result.Success, "C# 6 interpolation should not compile on the C# 5 compiler.");
            StringAssert.Contains("CS1056", string.Join(" ", result.CompilationErrors));
        }

        private static CompilerParameters Parameters(params string[] references)
        {
            var parameters = new CompilerParameters { GenerateInMemory = true, GenerateExecutable = false };
            parameters.ReferencedAssemblies.AddRange(references);
            return parameters;
        }

        private static HashSet<string> Identities(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }

        private static string ShadowCopyOf(Assembly assembly)
        {
            var directory = Path.Combine(Path.GetTempPath(), "PlayniteBridge-eval-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var copy = Path.Combine(directory, Path.GetFileName(assembly.Location));
            File.Copy(assembly.Location, copy);
            return copy;
        }
    }
}
