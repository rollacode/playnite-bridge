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
    /// Guards the compiler reference set against CS1703 (the same assembly identity
    /// imported twice) and CS1704 (the same simple name in two versions). Both break
    /// every /api/eval call before any user code is parsed.
    /// </summary>
    [TestFixture]
    public class EvalServiceReferenceTests
    {
        private const string BaseDirectory = @"C:\Playnite";
        private const string PluginDirectory = @"C:\Playnite\Extensions\some_plugin";

        // --- reference selection ---

        [Test]
        public void SelectDistinctReferences_SameIdentityTwoLocations_KeepsAppBaseCopy()
        {
            var appBase = Candidate("System.ValueTuple", "4.0.3.0",
                Path.Combine(BaseDirectory, "System.ValueTuple.dll"), true);
            var pluginCopy = Candidate("System.ValueTuple", "4.0.3.0",
                Path.Combine(PluginDirectory, "System.ValueTuple.dll"), false);

            var refs = EvalService.SelectDistinctReferences(new[] { appBase, pluginCopy });
            Assert.AreEqual(1, refs.Length, "The duplicate identity must collapse to one file.");
            Assert.AreEqual(appBase.Location, refs[0]);

            var reversed = EvalService.SelectDistinctReferences(new[] { pluginCopy, appBase });
            Assert.AreEqual(1, reversed.Length);
            Assert.AreEqual(appBase.Location, reversed[0], "Order of discovery must not decide the winner.");
        }

        [Test]
        public void SelectDistinctReferences_SameNameDifferentVersions_KeepsHighestVersion()
        {
            var older = Candidate("System.Runtime.CompilerServices.Unsafe", "4.0.4.1",
                Path.Combine(BaseDirectory, "System.Runtime.CompilerServices.Unsafe.dll"), false);
            var newer = Candidate("System.Runtime.CompilerServices.Unsafe", "6.0.3.0",
                Path.Combine(PluginDirectory, "System.Runtime.CompilerServices.Unsafe.dll"), false);

            var refs = EvalService.SelectDistinctReferences(new[] { older, newer });
            Assert.AreEqual(1, refs.Length, "CS1704 requires one reference per simple name.");
            Assert.AreEqual(newer.Location, refs[0]);

            Assert.AreEqual(newer.Location,
                EvalService.SelectDistinctReferences(new[] { newer, older })[0]);
        }

        [Test]
        public void SelectDistinctReferences_AppBaseWinsOverNewerPluginCopy()
        {
            var appBase = Candidate("System.Memory", "4.0.1.1",
                Path.Combine(BaseDirectory, "System.Memory.dll"), true);
            var pluginCopy = Candidate("System.Memory", "4.0.2.0",
                Path.Combine(PluginDirectory, "System.Memory.dll"), false);

            var refs = EvalService.SelectDistinctReferences(new[] { pluginCopy, appBase });
            Assert.AreEqual(appBase.Location, refs[0],
                "The copy the runtime actually bound to is the safest reference.");
        }

        [Test]
        public void SelectDistinctReferences_DifferentNames_KeepsAll()
        {
            var refs = EvalService.SelectDistinctReferences(new[]
            {
                Candidate("System", "4.0.0.0", Path.Combine(BaseDirectory, "System.dll"), true),
                Candidate("Playnite.SDK", "6.15.0.0", Path.Combine(PluginDirectory, "Playnite.SDK.dll"), false),
            });

            Assert.AreEqual(2, refs.Length);
        }

        [Test]
        public void SelectDistinctReferences_IdenticalPath_AddedOnce()
        {
            var path = Path.Combine(BaseDirectory, "System.dll");
            var refs = EvalService.SelectDistinctReferences(new[]
            {
                Candidate("System", "4.0.0.0", path, true),
                Candidate("System", "4.0.0.0", path.ToUpperInvariant(), true),
            });

            Assert.AreEqual(1, refs.Length);
        }

        [Test]
        public void BuildReferenceList_LoadedAssemblies_HaveNoDuplicateNames()
        {
            var refs = EvalService.BuildReferenceList(
                AppDomain.CurrentDomain.GetAssemblies(),
                AppDomain.CurrentDomain.BaseDirectory);

            var names = new List<string>();
            foreach (var path in refs)
                names.Add(AssemblyName.GetAssemblyName(path).Name);

            Assert.AreEqual(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                "Two references sharing a simple name is exactly what csc rejects.");
        }

        // --- app folder detection ---

        [Test]
        public void IsInDirectory_AppFolderVsExtensionFolder()
        {
            Assert.IsTrue(EvalService.IsInDirectory(Path.Combine(BaseDirectory, "Newtonsoft.Json.dll"), BaseDirectory));

            Assert.IsFalse(EvalService.IsInDirectory(Path.Combine(PluginDirectory, "Newtonsoft.Json.dll"), BaseDirectory),
                "Extension folders sit under the app folder but are not the app's own copy.");
        }

        [Test]
        public void IsInDirectory_IgnoresTrailingSeparatorAndCase()
        {
            Assert.IsTrue(EvalService.IsInDirectory(@"C:\Playnite\System.ValueTuple.dll", @"c:\playnite\"));
            Assert.IsFalse(EvalService.IsInDirectory(@"C:\Playnite2\System.dll", @"C:\Playnite"));
            Assert.IsFalse(EvalService.IsInDirectory(null, BaseDirectory));
            Assert.IsFalse(EvalService.IsInDirectory(Path.Combine(BaseDirectory, "x.dll"), null));
        }

        [Test]
        public void SelectDistinctReferences_ExtensionCopyDoesNotCountAsAppCopy()
        {
            // Both live under the app folder; only the first is the app's own file.
            var appCopy = new ReferenceCandidate
            {
                SimpleName = "Newtonsoft.Json",
                Identity = "Newtonsoft.Json, Version=10.0.0.0",
                Location = Path.Combine(BaseDirectory, "Newtonsoft.Json.dll"),
                Version = new Version(10, 0, 0, 0),
                IsInBaseDirectory = EvalService.IsInDirectory(Path.Combine(BaseDirectory, "Newtonsoft.Json.dll"), BaseDirectory),
            };
            var extensionCopy = new ReferenceCandidate
            {
                SimpleName = "Newtonsoft.Json",
                Identity = "Newtonsoft.Json, Version=13.0.0.0",
                Location = Path.Combine(PluginDirectory, "Newtonsoft.Json.dll"),
                Version = new Version(13, 0, 0, 0),
                IsInBaseDirectory = EvalService.IsInDirectory(Path.Combine(PluginDirectory, "Newtonsoft.Json.dll"), BaseDirectory),
            };

            var refs = EvalService.SelectDistinctReferences(new[] { extensionCopy, appCopy });

            Assert.AreEqual(appCopy.Location, refs[0],
                "The version the process actually loaded must win over a newer extension copy.");
        }

        private static ReferenceCandidate Candidate(string simpleName, string version, string location, bool inBaseDirectory)
        {
            var name = new AssemblyName(simpleName) { Version = new Version(version) };
            return new ReferenceCandidate
            {
                SimpleName = simpleName,
                Identity = name.FullName,
                Location = location,
                Version = name.Version,
                IsInBaseDirectory = inBaseDirectory,
            };
        }
    }
}
