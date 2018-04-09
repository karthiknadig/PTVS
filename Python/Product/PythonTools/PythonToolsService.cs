// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools {

    /// <summary>
    /// Provides services and state which need to be available to various PTVS components.
    /// </summary>
    public sealed class PythonToolsService : IDisposable {
        private readonly IServiceContainer _container;
        private readonly Lazy<LanguagePreferences> _langPrefs;
        private IPythonToolsOptionsService _optionsService;
        private Lazy<IInterpreterOptionsService> _interpreterOptionsService;
        private Lazy<IInterpreterRegistryService> _interpreterRegistryService;
        private readonly ConcurrentDictionary<string, VsProjectAnalyzer> _analyzers;
        private readonly IPythonToolsLogger _logger;
        private readonly Lazy<AdvancedEditorOptions> _advancedOptions;
        private readonly Lazy<DebuggerOptions> _debuggerOptions;
        private readonly Lazy<Options.ExperimentalOptions> _experimentalOptions;
        private readonly Lazy<DiagnosticsOptions> _diagnosticsOptions;
        private readonly Lazy<GeneralOptions> _generalOptions;
        private readonly Lazy<PythonInteractiveOptions> _debugInteractiveOptions;
        private readonly Lazy<PythonInteractiveOptions> _interactiveOptions;
        private readonly Lazy<SuppressDialogOptions> _suppressDialogOptions;
        private readonly IdleManager _idleManager;
        private readonly DiagnosticsProvider _diagnosticsProvider;
        private ExpansionCompletionSource _expansionCompletions;
        private Func<CodeFormattingOptions> _optionsFactory;
        private const string _formattingCat = "Formatting";

        private readonly Dictionary<IVsCodeWindow, CodeWindowManager> _codeWindowManagers = new Dictionary<IVsCodeWindow, CodeWindowManager>();

        private static readonly Dictionary<string, OptionInfo> _allFormattingOptions = new Dictionary<string, OptionInfo>();

        public static object CreateService(IServiceContainer container, Type serviceType) {
            if (serviceType.IsEquivalentTo(typeof(PythonToolsService))) {
                // register our PythonToolsService which provides access to core PTVS functionality
                try {
                    return new PythonToolsService(container);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    ex.ReportUnhandledException(container, typeof(PythonToolsService), allowUI: false);
                    throw;
                }
            }
            return null;
        }

        internal PythonToolsService(IServiceContainer container) {
            _container = container;
            _analyzers = new ConcurrentDictionary<string, VsProjectAnalyzer>();

            _langPrefs = new Lazy<LanguagePreferences>(() => new LanguagePreferences(this, typeof(PythonLanguageInfo).GUID));
            _interpreterOptionsService = new Lazy<IInterpreterOptionsService>(Site.GetComponentModel().GetService<IInterpreterOptionsService>);
            _interpreterRegistryService = new Lazy<IInterpreterRegistryService>(Site.GetComponentModel().GetService<IInterpreterRegistryService>);

            _optionsService = (IPythonToolsOptionsService)container.GetService(typeof(IPythonToolsOptionsService));

            _idleManager = new IdleManager(container);
            _advancedOptions = new Lazy<AdvancedEditorOptions>(CreateAdvancedEditorOptions);
            _debuggerOptions = new Lazy<DebuggerOptions>(CreateDebuggerOptions);
            _experimentalOptions = new Lazy<Options.ExperimentalOptions>(CreateExperimentalOptions);
            _diagnosticsOptions = new Lazy<DiagnosticsOptions>(CreateDiagnosticsOptions);
            _generalOptions = new Lazy<GeneralOptions>(CreateGeneralOptions);
            _suppressDialogOptions = new Lazy<SuppressDialogOptions>(() => new SuppressDialogOptions(this));
            _interactiveOptions = new Lazy<PythonInteractiveOptions>(() => CreateInteractiveOptions("Interactive"));
            _debugInteractiveOptions = new Lazy<PythonInteractiveOptions>(() => CreateInteractiveOptions("Debug Interactive Window"));
            _logger = (IPythonToolsLogger)container.GetService(typeof(IPythonToolsLogger));
            _diagnosticsProvider = new DiagnosticsProvider(container);

            _idleManager.OnIdle += OnIdleInitialization;

            EditorServices.SetPythonToolsService(this);
        }

        private void OnIdleInitialization(object sender, ComponentManagerEventArgs e) {
            Site.AssertShellIsInitialized();

            _idleManager.OnIdle -= OnIdleInitialization;

            _expansionCompletions = new ExpansionCompletionSource(Site);
            InitializeLogging();
        }

        public void Dispose() {
            if (_langPrefs.IsValueCreated) {
                _langPrefs.Value.Dispose();
            }

            _idleManager.Dispose();

            foreach (var window in _codeWindowManagers.Values.ToArray()) {
                window.RemoveAdornments();
            }
            _codeWindowManagers.Clear();

            foreach (var kv in GetActiveSharedAnalyzers()) {
                kv.Value.Dispose();
            }
        }

        private void InitializeLogging() {
            try {
                var registry = ComponentModel.GetService<IInterpreterRegistryService>();
                if (registry != null) { // not available in some test cases...
                                                    // log interesting stats on startup
                    var installed = registry.Configurations.Count();
                    var installedV2 = registry.Configurations.Count(c => c.Version.Major == 2);
                    var installedV3 = registry.Configurations.Count(c => c.Version.Major == 3);

                    _logger.LogEvent(PythonLogEvent.InstalledInterpreters, new Dictionary<string, object> {
                        { "Total", installed },
                        { "3x", installedV3 },
                        { "2x", installedV2 }
                    });
                }

                _logger.LogEvent(PythonLogEvent.Experiments, new Dictionary<string, object> {
                    { "NoDatabaseFactory", ExperimentalOptions.NoDatabaseFactory },
                    { "AutoDetectCondaEnvironments", ExperimentalOptions.AutoDetectCondaEnvironments },
                    { "UseCondaPackageManager", ExperimentalOptions.UseCondaPackageManager },
                    { "UseVsCodeDebugger", ExperimentalOptions.UseVsCodeDebugger },
                    { "UseDockerContainer", ExperimentalOptions.UseDockerContainer }
                });
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }
        }

        /// <summary>
        /// Asks the interpreter to generate its completion database if the
        /// option is enabled (the default) and the database is not current.
        /// </summary>
        internal void EnsureCompletionDb(IPythonInterpreterFactory factory) {
            if (GeneralOptions.AutoAnalyzeStandardLibrary) {
                var withDb = factory as Interpreter.LegacyDB.IPythonInterpreterFactoryWithDatabase;
                if (withDb != null && !withDb.IsCurrent) {
                    withDb.GenerateDatabase(Interpreter.LegacyDB.GenerateDatabaseOptions.SkipUnchanged);
                }
            }
        }

        internal PythonEditorServices EditorServices => ComponentModel.GetService<PythonEditorServices>();

        internal void GetDiagnosticsLog(TextWriter writer, bool includeAnalysisLogs) {
            _diagnosticsProvider.WriteLog(writer, includeAnalysisLogs);
        }

        internal IInterpreterOptionsService InterpreterOptionsService => _interpreterOptionsService.Value;
        internal IInterpreterRegistryService InterpreterRegistryService => _interpreterRegistryService.Value;

        internal IPythonToolsLogger Logger => _logger;

        internal Task<VsProjectAnalyzer> CreateAnalyzerAsync(IPythonInterpreterFactory factory) {
            if (factory == null) {
                return VsProjectAnalyzer.CreateDefaultAsync(EditorServices, InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7)));
            }
            EnsureCompletionDb(factory);
            return VsProjectAnalyzer.CreateDefaultAsync(EditorServices, factory);
        }

        #region Public API

        /// <summary>
        /// <para>Gets a shared analyzer for a given environment.</para>
        /// <para>When the analyzer is no longer required, call
        /// <see cref="VsProjectAnalyzer.RemoveUser"/> and if it returns
        /// <c>true</c>, call <see cref="VsProjectAnalyzer.Dispose"/>.</para>
        /// </summary>
        internal async Task<VsProjectAnalyzer> GetSharedAnalyzerAsync(IPythonInterpreterFactory factory = null) {
            var result = TryGetSharedAnalyzer(factory, out var id);
            if (result != null) {
                return result;
            }

            result = await CreateAnalyzerAsync(factory);
            var realResult = _analyzers.GetOrAdd(id, result);
            if (realResult != result && result.RemoveUser()) {
                result.Dispose();
            }
            return realResult;
        }

        /// <summary>
        /// Gets an active shared analyzer if one exists and can be
        /// obtained without blocking. If this returns non-null and
        /// <paramref name="addUser"/> is <c>true</c>, the caller
        /// is responsible to call <see cref="VsProjectAnalyzer.RemoveUser"/>
        /// and if necessary, <see cref="VsProjectAnalyzer.Dispose"/>.
        /// </summary>
        internal VsProjectAnalyzer TryGetSharedAnalyzer(IPythonInterpreterFactory factory, out string id, bool addUser = true) {
            id = factory?.Configuration?.Id;
            if (string.IsNullOrEmpty(id)) {
                factory = _interpreterOptionsService.Value?.DefaultInterpreter;
                id = _interpreterOptionsService.Value?.DefaultInterpreterId ?? string.Empty;
            }

            if (_analyzers.TryGetValue(id, out var result)) {
                try {
                    result.AddUser();
                    return result;
                } catch (ObjectDisposedException) {
                    _analyzers.TryRemove(id, out _);
                }
            }

            return null;
        }

        internal IEnumerable<KeyValuePair<string, VsProjectAnalyzer>> GetActiveSharedAnalyzers() {
            return _analyzers.ToArray();
        }

        internal IEnumerable<KeyValuePair<string, VsProjectAnalyzer>> GetActiveAnalyzers() {
            foreach (var kv in GetActiveSharedAnalyzers()) {
                var config = _interpreterRegistryService.Value.FindConfiguration(kv.Key);
                yield return new KeyValuePair<string, VsProjectAnalyzer>(config?.Description ?? kv.Key, kv.Value);
            }

            var sln = (IVsSolution)Site.GetService(typeof(SVsSolution));
            foreach (var proj in sln.EnumerateLoadedPythonProjects()) {
                var analyzer = proj.TryGetAnalyzer();
                if (analyzer != null) {
                    yield return new KeyValuePair<string, VsProjectAnalyzer>(proj.Caption, analyzer);
                }
            }
        }

        public AdvancedEditorOptions AdvancedOptions => _advancedOptions.Value;
        public DebuggerOptions DebuggerOptions => _debuggerOptions.Value;
        public Options.ExperimentalOptions ExperimentalOptions => _experimentalOptions.Value;
        public DiagnosticsOptions DiagnosticsOptions => _diagnosticsOptions.Value;
        public GeneralOptions GeneralOptions => _generalOptions.Value;
        internal PythonInteractiveOptions DebugInteractiveOptions => _debugInteractiveOptions.Value;

        private AdvancedEditorOptions CreateAdvancedEditorOptions() {
            var opts = new AdvancedEditorOptions(this);
            opts.Load();
            return opts;
        }

        private DebuggerOptions CreateDebuggerOptions() {
            var opts = new DebuggerOptions(this);
            opts.Load();
            return opts;
        }

        private Options.ExperimentalOptions CreateExperimentalOptions() {
            var opts = new Options.ExperimentalOptions(this);
            opts.Load();
            return opts;
        }

        private DiagnosticsOptions CreateDiagnosticsOptions() {
            var opts = new DiagnosticsOptions(this);
            opts.Load();
            return opts;
        }

        private GeneralOptions CreateGeneralOptions() {
            var opts = new GeneralOptions(this);
            opts.Load();
            return opts;
        }

        #endregion

        internal SuppressDialogOptions SuppressDialogOptions => _suppressDialogOptions.Value;

        #region Code formatting options

        /// <summary>
        /// Gets a new CodeFormattinOptions object configured to the users current settings.
        /// </summary>
        public CodeFormattingOptions GetCodeFormattingOptions() {
            if (_optionsFactory == null) {
                // create a factory which can create CodeFormattingOptions without tons of reflection
                var initializers = new Dictionary<OptionInfo, Action<CodeFormattingOptions, object>>();
                foreach (CodeFormattingCategory curCat in Enum.GetValues(typeof(CodeFormattingCategory))) {
                    if (curCat == CodeFormattingCategory.None) {
                        continue;
                    }

                    var cat = OptionCategory.GetOptions(curCat);
                    foreach (var option in cat) {
                        var propInfo = typeof(CodeFormattingOptions).GetProperty(option.Key);

                        if (propInfo.PropertyType == typeof(bool)) {
                            initializers[option] = MakeFastSetter<bool>(propInfo);
                        } else if (propInfo.PropertyType == typeof(bool?)) {
                            initializers[option] = MakeFastSetter<bool?>(propInfo);
                        } else if (propInfo.PropertyType == typeof(int)) {
                            initializers[option] = MakeFastSetter<int>(propInfo);
                        } else {
                            throw new InvalidOperationException(String.Format("Unsupported formatting option type: {0}", propInfo.PropertyType.FullName));
                        }
                    }
                }

                _optionsFactory = CreateOptionsFactory(initializers);
            }

            return _optionsFactory();
        }

        private static Action<CodeFormattingOptions, object> MakeFastSetter<T>(PropertyInfo propInfo) {
            var fastSet = (Action<CodeFormattingOptions, T>)Delegate.CreateDelegate(typeof(Action<CodeFormattingOptions, T>), propInfo.GetSetMethod());
            return (options, value) => fastSet(options, (T)value);
        }

        private Func<CodeFormattingOptions> CreateOptionsFactory(Dictionary<OptionInfo, Action<CodeFormattingOptions, object>> initializers) {
            return () => {
                var res = new CodeFormattingOptions();
                foreach (var keyValue in initializers) {
                    var option = keyValue.Key;
                    var fastSet = keyValue.Value;

                    fastSet(res, option.DeserializeOptionValue(LoadString(option.Key, _formattingCat)));
                }
                return res;
            };
        }

        /// <summary>
        /// Sets the value for a formatting setting.  The name is one of the properties
        /// in CodeFormattingOptions.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetFormattingOption(string name, object value) {
            EnsureAllOptions();
            OptionInfo option;
            if (!_allFormattingOptions.TryGetValue(name, out option)) {
                throw new InvalidOperationException("Unknown option " + name);
            }

            SaveString(name, _formattingCat, option.SerializeOptionValue(value));
        }

        /// <summary>
        /// Gets the value for a formatting setting.  The name is one of the properties in
        /// CodeFormattingOptions.
        /// </summary>
        public object GetFormattingOption(string name) {
            EnsureAllOptions();
            OptionInfo option;
            if (!_allFormattingOptions.TryGetValue(name, out option)) {
                throw new InvalidOperationException("Unknown option " + name);
            }
            return option.DeserializeOptionValue(LoadString(name, _formattingCat));
        }

        private static void EnsureAllOptions() {
            if (_allFormattingOptions.Count == 0) {
                foreach (CodeFormattingCategory curCat in Enum.GetValues(typeof(CodeFormattingCategory))) {
                    if (curCat == CodeFormattingCategory.None) {
                        continue;
                    }

                    var cat = OptionCategory.GetOptions(curCat);
                    foreach (var optionInfo in cat) {
                        _allFormattingOptions[optionInfo.Key] = optionInfo;
                    }
                }
            }
        }

        #endregion

        #region Interactive Options

        internal PythonInteractiveOptions InteractiveOptions => _interactiveOptions.Value;

        /// <summary>
        /// Interactive window backend. If set, it overrides the value in the
        /// mode.txt file. For use by tests, rather than have them modify
        /// mode.txt directly.
        /// </summary>
        internal string InteractiveBackendOverride { get; set; }

        private PythonInteractiveOptions CreateInteractiveOptions(string category) {
            var opts = new PythonInteractiveOptions(this, category);
            opts.Load();
            return opts;
        }

        #endregion

        internal IComponentModel ComponentModel {
            get {
                return (IComponentModel)_container.GetService(typeof(SComponentModel));
            }
        }

        internal System.IServiceProvider Site => _container;

        internal LanguagePreferences LangPrefs => _langPrefs.Value;

        /// <summary>
        /// Ensures the shell is loaded before returning language preferences,
        /// as obtaining them while the shell is initializing can corrupt
        /// settings.
        /// </summary>
        /// <remarks>
        /// Should only be called from the UI thread, and you must not
        /// synchronously wait on the returned task.
        /// </remarks>
        internal async Task<LanguagePreferences> GetLangPrefsAsync() {
            if (_langPrefs.IsValueCreated) {
                return _langPrefs.Value;
            }
            await _container.WaitForShellInitializedAsync();
            return _langPrefs.Value;
        }

        #region Registry Persistance

        internal void DeleteCategory(string category) {
            _optionsService.DeleteCategory(category);
        }

        internal void SaveBool(string name, string category, bool value) {
            SaveString(name, category, value.ToString());
        }

        internal void SaveInt(string name, string category, int value) {
            SaveString(name, category, value.ToString());
        }

        internal void SaveString(string name, string category, string value) {
            _optionsService.SaveString(name, category, value);
        }

        internal string LoadString(string name, string category) {
            return _optionsService.LoadString(name, category);
        }

        internal void SaveEnum<T>(string name, string category, T value) where T : struct {
            SaveString(name, category, value.ToString());
        }

        internal void SaveDateTime(string name, string category, DateTime value) {
            SaveString(name, category, value.ToString(CultureInfo.InvariantCulture));
        }

        internal int? LoadInt(string name, string category) {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            int val;
            if (int.TryParse(res, out val)) {
                return val;
            }
            return null;
        }

        internal bool? LoadBool(string name, string category) {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            bool val;
            if (bool.TryParse(res, out val)) {
                return val;
            }
            return null;
        }

        internal T? LoadEnum<T>(string name, string category) where T : struct {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            T enumRes;
            if (Enum.TryParse<T>(res, out enumRes)) {
                return enumRes;
            }
            return null;
        }

        internal DateTime? LoadDateTime(string name, string category) {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            DateTime dateRes;
            if (DateTime.TryParse(res, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateRes)) {
                return dateRes;
            }
            return null;
        }

        #endregion

        #region Idle processing

        internal event EventHandler<ComponentManagerEventArgs> OnIdle {
            add {
                lock (_idleManager) {
                    _idleManager.OnIdle += value;
                }
            }
            remove {
                lock (_idleManager) {
                    _idleManager.OnIdle -= value;
                }
            }
        }

        #endregion

        #region Language Preferences

        internal LANGPREFERENCES2 GetLanguagePreferences() {
            var txtMgr = (IVsTextManager2)_container.GetService(typeof(SVsTextManager));
            var langPrefs = new[] { new LANGPREFERENCES2 { guidLang = GuidList.guidPythonLanguageServiceGuid } };
            ErrorHandler.ThrowOnFailure(txtMgr.GetUserPreferences2(null, null, langPrefs, null));
            return langPrefs[0];
        }

        internal void SetLanguagePreferences(LANGPREFERENCES2 langPrefs) {
            var txtMgr = (IVsTextManager2)_container.GetService(typeof(SVsTextManager));
            ErrorHandler.ThrowOnFailure(txtMgr.SetUserPreferences2(null, null, new[] { langPrefs }, null));
        }

        internal IEnumerable<CodeWindowManager> CodeWindowManagers {
            get {
                return _codeWindowManagers.Values;
            }
        }

        internal CodeWindowManager GetOrCreateCodeWindowManager(IVsCodeWindow window) {
            CodeWindowManager value;
            if (!_codeWindowManagers.TryGetValue(window, out value)) {
                _codeWindowManagers[window] = value = new CodeWindowManager(_container, window);

            }
            return value;
        }

        internal void CodeWindowClosed(IVsCodeWindow window) {
            _codeWindowManagers.Remove(window);
        }
        #endregion

        #region Intellisense

        internal CompletionAnalysis GetCompletions(ICompletionSession session, ITextView view, ITextSnapshot snapshot, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
            return VsProjectAnalyzer.GetCompletions(EditorServices, session, view, snapshot, span, point, options);
        }

        internal SignatureAnalysis GetSignatures(ITextView view, ITextSnapshot snapshot, ITrackingSpan span) {
            var entry = snapshot.TextBuffer.TryGetAnalysisEntry();
            if (entry == null) {
                return new SignatureAnalysis("", 0, new ISignature[0]);
            }
            return entry.Analyzer.WaitForRequest(entry.Analyzer.GetSignaturesAsync(entry, view, snapshot, span), "GetSignatures");
        }

        internal Task<SignatureAnalysis> GetSignaturesAsync(ITextView view, ITextSnapshot snapshot, ITrackingSpan span) {
            var entry = snapshot.TextBuffer.TryGetAnalysisEntry();
            if (entry == null) {
                return Task.FromResult(new SignatureAnalysis("", 0, new ISignature[0]));
            }
            return entry.Analyzer.GetSignaturesAsync(entry, view, snapshot, span);
        }

        internal Task<IEnumerable<CompletionResult>> GetExpansionCompletionsAsync() {
            if (_expansionCompletions == null) {
                return Task.FromResult<IEnumerable<CompletionResult>>(null);
            }
            return _expansionCompletions.GetCompletionsAsync();
        }

        #endregion

        internal Dictionary<string, string> GetFullEnvironment(LaunchConfiguration config) {
            if (config == null) {
                throw new ArgumentNullException(nameof(config));
            }

            // Start with global environment, add configured environment,
            // then add search paths.
            var baseEnv = Environment.GetEnvironmentVariables();
            // Clear search paths from the global environment. The launch
            // configuration should include the existing value

            var pathVar = config.Interpreter?.PathEnvironmentVariable;
            if (string.IsNullOrEmpty(pathVar)) {
                pathVar = "PYTHONPATH";
            }
            baseEnv[pathVar] = string.Empty;
            var env = PathUtils.MergeEnvironments(
                baseEnv.AsEnumerable<string, string>(),
                config.GetEnvironmentVariables(),
                "Path", pathVar
            );
            if (config.SearchPaths != null && config.SearchPaths.Any()) {
                env = PathUtils.MergeEnvironments(
                    env,
                    new[] {
                        new KeyValuePair<string, string>(
                            pathVar,
                            PathUtils.JoinPathList(config.SearchPaths)
                        )
                    },
                    pathVar
                );
            }
            return env;
        }

        internal IEnumerable<string> GetGlobalPythonSearchPaths(InterpreterConfiguration interpreter) {
            if (!GeneralOptions.ClearGlobalPythonPath) {
                string pythonPath = Environment.GetEnvironmentVariable(interpreter.PathEnvironmentVariable) ?? string.Empty;
                return pythonPath
                    .Split(Path.PathSeparator)
                    // Just ensure the string is not empty - if people are passing
                    // through invalid paths this option is meant to allow it
                    .Where(p => !string.IsNullOrEmpty(p));
            }

            return Enumerable.Empty<string>();
        }
    }
}
