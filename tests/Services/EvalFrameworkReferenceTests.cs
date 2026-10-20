namespace PlayniteBridge.Tests.Services
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CSharp;
    using NUnit.Framework;
    using PlayniteBridge.Services;

    [TestFixture]
    public class EvalFrameworkReferenceTests
    {
        private const string RuntimeIdentity = "System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
        private const string ProbeSource = "public static class Probe { public static int Run() { return 1; } }";

        [Test]
        public void WindowsMetadata_ReproducesMissingRuntimeFacade()
        {
            var metadata = MetadataPath("Windows.Foundation.winmd");
            var parameters = Parameters(metadata);
            using (var compiler = new CSharpCodeProvider())
            {
                var before = compiler.CompileAssemblyFromSource(parameters, ProbeSource);
                Assert.That(before.Errors.Cast<CompilerError>(), Has.Some.Matches<CompilerError>(error =>
                    error.ErrorNumber == "CS0012" && error.ErrorText.Contains("System.Runtime")));

                AddMetadataDependencies(parameters, metadata);
                var after = compiler.CompileAssemblyFromSource(parameters, ProbeSource);
                Assert.That(after.Errors.HasErrors, Is.False, Errors(after));
            }
        }

        [TestCase("Windows.Graphics.winmd")]
        [TestCase("Windows.Storage.winmd")]
        [TestCase("Windows.UI.winmd")]
        public void OtherWindowsMetadata_CompilesWithResolvedFrameworkDependencies(string fileName)
        {
            var metadata = MetadataPath(fileName);
            var parameters = Parameters(metadata);
            AddMetadataDependencies(parameters, metadata);
            using (var compiler = new CSharpCodeProvider())
            {
                var result = compiler.CompileAssemblyFromSource(parameters, ProbeSource);
                Assert.That(result.Errors.HasErrors, Is.False, Errors(result));
            }
        }

        [Test]
        public void FrameworkReferences_IncludeTransitiveDependenciesAndAreIdempotent()
        {
            var parameters = Parameters();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dependency = new AssemblyName(RuntimeIdentity);
            EvalService.AddFrameworkReferences(parameters, new[] { dependency, dependency }, identities);

            var names = parameters.ReferencedAssemblies.Cast<string>()
                .Select(path => AssemblyName.GetAssemblyName(path).Name).ToArray();
            Assert.That(names, Does.Contain("System.Runtime"));
            Assert.That(names, Does.Contain("System.Core"), "The facade's dependencies must also be referenced.");
            Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(names.Length));
            var count = parameters.ReferencedAssemblies.Count;
            EvalService.AddFrameworkReferences(parameters, new[] { dependency }, identities);
            Assert.That(parameters.ReferencedAssemblies.Count, Is.EqualTo(count));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingFrameworkReference_PreservesShadowCopyAndSelectedVersion(bool olderDependency)
        {
            var existing = typeof(Enumerable).Assembly;
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "PlayniteBridge-eval-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                var shadowCopy = Path.Combine(temporaryDirectory, Path.GetFileName(existing.Location));
                File.Copy(existing.Location, shadowCopy);
                var parameters = Parameters(shadowCopy);
                var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { existing.FullName };
                var dependency = olderDependency
                    ? new AssemblyName("System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    : existing.GetName();

                EvalService.AddFrameworkReferences(parameters, new[] { dependency }, identities);
                Assert.That(parameters.ReferencedAssemblies.Cast<string>(), Is.EqualTo(new[] { shadowCopy }),
                    "A GAC copy or older framework version must not duplicate the application's reference.");
            }
            finally
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void MissingOrPluginDependencies_AreNotAdded()
        {
            var parameters = Parameters();
            EvalService.AddFrameworkReferences(parameters, new[]
            {
                typeof(EvalService).Assembly.GetName(),
                new AssemblyName("MissingFrameworkFacade, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
            }, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.That(parameters.ReferencedAssemblies, Is.Empty);
        }

        private static CompilerParameters Parameters(params string[] references)
        {
            var parameters = new CompilerParameters { GenerateInMemory = true, GenerateExecutable = false };
            parameters.ReferencedAssemblies.AddRange(references);
            return parameters;
        }

        private static void AddMetadataDependencies(CompilerParameters parameters, string path)
        {
            var metadata = Assembly.ReflectionOnlyLoadFrom(path);
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                metadata.FullName,
                typeof(object).Assembly.FullName, // CodeDOM references mscorlib implicitly.
            };
            EvalService.AddFrameworkReferences(parameters, metadata.GetReferencedAssemblies(), identities);
        }

        private static string MetadataPath(string fileName)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WinMetadata", fileName);
            if (!File.Exists(path)) Assert.Ignore("This regression requires Windows metadata: " + fileName);
            return path;
        }

        private static string Errors(CompilerResults result) =>
            string.Join(Environment.NewLine, result.Errors.Cast<CompilerError>().Select(error => error.ToString()));
    }
}
