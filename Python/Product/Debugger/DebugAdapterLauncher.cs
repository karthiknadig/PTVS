﻿// Python Tools for Visual Studio
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    [ComVisible(true)]
    [Guid(DebugAdapterLauncherCLSIDNoBraces)]
    public sealed class DebugAdapterLauncher : IAdapterLauncher {
        public const string DebugAdapterLauncherCLSIDNoBraces = "C2990BF1-A87B-4459-9478-322482C535D6";
        public const string DebugAdapterLauncherCLSID = "{"+ DebugAdapterLauncherCLSIDNoBraces + "}";
        public const string VSCodeDebugEngineId = "{86432F39-ADFD-4C56-AA8F-AF8FCDC66039}";
        public static Guid VSCodeDebugEngine = new Guid(VSCodeDebugEngineId);

        public DebugAdapterLauncher(){}

        public void Initialize(IDebugAdapterHostContext context) {
        }

        public ITargetHostProcess LaunchAdapter(IAdapterLaunchInfo launchInfo, ITargetHostInterop targetInterop) {
            // ITargetHostInterop provides a convenience wrapper to start the process
            // return targetInterop.ExecuteCommandAsync(path, "");

            // If you need more control use the DebugAdapterProcess
            if(launchInfo.LaunchType == LaunchType.Attach) {
                return DebugAdapterRemoteProcess.Attach(launchInfo.LaunchJson);
            }
            return DebugAdapterProcess.Start(launchInfo.LaunchJson);
        }
        public void UpdateLaunchOptions(IAdapterLaunchInfo launchInfo) {
            if(launchInfo.LaunchType == LaunchType.Attach) {
                launchInfo.DebugPort.GetPortName(out string uri);
                JObject pathMapping1 = new JObject();
                pathMapping1["localRoot"] = @"c:\GIT\djangodocker\mydjangoproject\";
                pathMapping1["remoteRoot"] = "/home/kanadig/GIT/myproj/";
                JArray arr = new JArray(pathMapping1);

                JObject obj = new JObject {
                    ["remote"] = uri,
                    ["pathMappings"] = arr
                };
                launchInfo.LaunchJson = obj.ToString();
            } else {
                JObject pathMapping1 = new JObject();
                pathMapping1["localRoot"] = @"c:\GIT\djangodocker\mydjangoproject\";
                pathMapping1["remoteRoot"] = "/tmp/mydjangoproject/";

                //JObject pathMapping2 = new JObject();
                //pathMapping2["localRoot"] = @"C:/GIT/djangodocker/mydjangoproject/";
                //pathMapping2["remoteRoot"] = "/tmp/mydjangoproject/";

                //JObject pathMapping3 = new JObject();
                //pathMapping3["localRoot"] = @"c:\GIT\djangodocker\mydjangoproject\";
                //pathMapping3["remoteRoot"] = "/tmp/mydjangoproject/";

                //JObject pathMapping4 = new JObject();
                //pathMapping4["localRoot"] = @"c:/GIT/djangodocker/mydjangoproject/";
                //pathMapping4["remoteRoot"] = "/tmp/mydjangoproject/";

                JArray arr = new JArray(pathMapping1);
                JObject obj = JObject.Parse(launchInfo.LaunchJson);
                obj["pathMappings"] = arr;
                obj["options"] = obj["options"].Value<string>() + ";WINDOWS_CLIENT=True;FIX_FILE_PATH_CASE=True;";
                launchInfo.LaunchJson = obj.ToString();
            }
        }
    }
}
