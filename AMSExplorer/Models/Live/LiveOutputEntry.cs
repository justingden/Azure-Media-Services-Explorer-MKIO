﻿//----------------------------------------------------------------------------------------------
//    Copyright 2023 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//---------------------------------------------------------------------------------------------

using Azure.ResourceManager.Media.Models;
using System;
using System.Drawing;

namespace AMSExplorer
{
    public class LiveOutputEntry
    {
        public string Name { get; set; }
        public LiveOutputResourceState? State { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string Description { get; set; }
        public TimeSpan? ArchiveWindowLength { get; set; }
        public Bitmap Published { get; set; }
        public string LiveEventName { get; set; } // Name of live event where the output is attached

    }
}
