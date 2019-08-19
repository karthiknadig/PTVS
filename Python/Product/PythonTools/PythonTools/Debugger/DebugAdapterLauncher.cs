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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    [ComVisible(true)]
    [Guid(DebugAdapterLauncherCLSIDNoBraces)]
    public sealed class DebugAdapterLauncher : IAdapterLauncher {
        public const string DebugAdapterLauncherCLSIDNoBraces = "C2990BF1-A87B-4459-9478-322482C535D6";
        public const string DebugAdapterLauncherCLSID = "{" + DebugAdapterLauncherCLSIDNoBraces + "}";
        public const string VSCodeDebugEngineId = "{86432F39-ADFD-4C56-AA8F-AF8FCDC66039}";
        public static Guid VSCodeDebugEngine = new Guid(VSCodeDebugEngineId);

        private string PythonBin { get; set; }

        public DebugAdapterLauncher() { }

        public void Initialize(IDebugAdapterHostContext context) {
        }

        public ITargetHostProcess LaunchAdapter(IAdapterLaunchInfo launchInfo, ITargetHostInterop targetInterop) {
            var ptvsdAdapterDirectory = Path.GetDirectoryName(PythonToolsInstallPath.GetFile("Packages\\ptvsd\\adapter\\__init__.py"));
            return targetInterop.ExecuteCommandAsync(PythonBin, ptvsdAdapterDirectory);
        }

        public void UpdateLaunchOptions(IAdapterLaunchInfo launchInfo) {
            var launchJson = JObject.Parse(launchInfo.LaunchJson);

            if (launchInfo.LaunchType == LaunchType.Launch) {
                var config = launchJson.Value<JObject>("ConfigurationProperties");
                var json = config ?? launchJson;
                // Note that this can be a array or string:
                // string -> "C:\python37\python.exe"
                // array -> ["C:\python37\python.exe", "--interpreter-arg1", "--interpreter-arg2", ... ]
                launchJson["python"] = json.Value<string>("exe");
                PythonBin = json.Value<string>("exe");

                if (json.TryGetValue("program", out JToken program)) {
                    // Note that this can be a array or string:
                    // string -> "user_script.py"
                    // array -> ["user_script.py", "--user-arg1", "--user-arg2", ... ]
                    launchJson["program"] = program.Value<string>();
                    // args can be a string or array.
                    launchJson["args"] = json.GetValue("args");
                } else if (launchJson.TryGetValue("module", out JToken module)) {
                    launchJson["module"] = module.Value<string>();
                    launchJson["args"] = json.GetValue("args");
                } else if (launchJson.TryGetValue("code", out JToken code)) {
                    launchJson["code"] = code.Value<string>();
                    launchJson["args"] = json.GetValue("args");
                } else {
                    launchJson["program"] = json.GetValue("args");
                    launchJson["args"] = new JArray();
                }

                var env = new JObject();
                foreach (var e in json.Value<JArray>("env")) {
                    env[e.Value<string>("name")] = e.Value<string>("value");
                }
                launchJson["env"] = env;
                launchJson["cwd"] = json.Value<string>("cwd");

                if (config != null) {
                    launchJson.Remove("ConfigurationProperties");
                }
            } else {
                PythonBin = launchJson.Value<string>("exe");

                // For Attach to process all we need is the pid
                if (launchJson.TryGetValue("processId", out JToken pid)) {
                    launchJson["processId"] = pid.Value<int>();
                    // NOTE: We don't need host and port because the injected debugger 
                    // connects back to the debug adapter. This allows us to re-attach
                    // to the same process without side effects. If we use host and port 
                    // after detach there is no easy way to say if the port is already
                    // opened in the debuggee. This might result is debuggee crash on 
                    // re-attach.
                } else {
                    launchInfo.DebugPort.GetPortName(out string uristr);
                    var uri = new Uri(uristr);

                    launchJson["host"] = uri.Host;
                    launchJson["port"] = uri.Port;
                }
            }

            AddDebugOptions(launchJson);
            AddRules(launchJson);
            launchInfo.LaunchJson = launchJson.ToString();
        }

        private void AddDebugOptions(JObject launchJson) {
            var debugService = (IPythonDebugOptionsService)Package.GetGlobalService(typeof(IPythonDebugOptionsService));

            // Stop on entry should always be true for VS Debug Adapter Host.
            // If stop on entry is disabled then VS will automatically issue
            // contnue when it sees "stopped" event with "reason=entry".
            launchJson["stopOnEntry"] = true;

            launchJson["showReturnValue"] = debugService.ShowFunctionReturnValue;
            launchJson["breakOnSystemExitZero"] = debugService.BreakOnSystemExitZero;
            launchJson["waitOnAbnormalExit"] = debugService.WaitOnAbnormalExit;
            launchJson["waitOnNormalExit"] = debugService.WaitOnNormalExit;
            launchJson["redirectOutput"] = debugService.TeeStandardOutput;
        }

        private void AddRules(JObject launchJson) {
            string ptvsdDirectory = PathUtils.GetParent(typeof(DebugAdapterLauncher).Assembly.Location);

            var rules = new JArray();
            var excludePTVSDirectory = new JObject() {
                ["path"] = Path.Combine(ptvsdDirectory, "**"),
                ["include"] = false,
            };

            rules.Add(excludePTVSDirectory);

            launchJson["rules"] = rules;
        }
    }
}
