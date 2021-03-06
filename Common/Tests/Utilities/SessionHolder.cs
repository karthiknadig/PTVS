﻿// Visual Studio Shared Project
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities {
    public class SessionHolder<T> : IDisposable where T : IIntellisenseSession {
        public readonly T Session;
        private readonly IEditor _owner;

        public SessionHolder(T session, IEditor owner) {
            Assert.IsNotNull(session);
            Session = session;
            _owner = owner;
        }

        void IDisposable.Dispose() {
            if (!Session.IsDismissed) {
                _owner.Invoke(() => { Session.Dismiss(); });
            }
        }
    }

}
