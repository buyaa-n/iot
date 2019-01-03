﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Device.Gpio
{
    class SafeLineHandle : SafeHandle
    {
        internal SafeLineHandle() : base(IntPtr.Zero, false) { }

        override protected bool ReleaseHandle()
        {
            return true;
        }

        public override bool IsInvalid
        {
            get
            {
                return handle == IntPtr.Zero || handle == new IntPtr(-1);
            }
        }
    }
}
